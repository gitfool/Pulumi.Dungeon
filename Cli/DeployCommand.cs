using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Json.More;
using Json.Path;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulumi.Automation;
using Pulumi.Dungeon.Aws;
using Pulumi.Dungeon.K8s;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public sealed partial class DeployCommand : AsyncCommandBase<DeployCommand.Settings>
    {
        public DeployCommand(IOptions<Config> options, ILoggerFactory loggerFactory, ILogger<DeployCommand> logger, IServiceProvider serviceProvider)
            : base(options, loggerFactory, logger)
        {
            ServiceProvider = serviceProvider;

            ResourceInfo = new()
            {
                [Resources.AwsEks] = new()
                {
                    ProjectName = "aws-eks",
                    StackType = typeof(EksStack)
                },
                [Resources.K8s] = new()
                {
                    ProjectName = "k8s",
                    StackType = typeof(K8sStack)
                }
            };

            RequiredPlugins = new[] { "aws v4.14.0", "kubernetes v3.5.2", "random v4.2.0", "tls v4.0.0" };
        }

        protected override async Task<int> OnExecuteAsync(CommandContext context, Settings settings)
        {
            Logger.LogInformation("Deploying resources");
            using var totalTimeLogger = new ElapsedTimeLogger(Logger, "Deployed resources");

            foreach (var resource in settings.Resources.ToOrderedArray())
            {
                var info = ResourceInfo[resource];
                var stackFullName = $"{Config.Pulumi.Organization.Name}/{info.ProjectName}/{settings.Environment.ToLower()}";
                Logger.LogInformation($"Deploying {stackFullName}");
                using var resourceTimeLogger = new ElapsedTimeLogger(Logger, $"Deployed {stackFullName}");

                var stackName = $"{Config.Pulumi.Organization.Name}/{settings.Environment.ToLower()}";
                var stackArgs = new InlineProgramArgs(info.ProjectName, stackName, PulumiFn.Create(ServiceProvider, info.StackType))
                {
                    Logger = LoggerFactory.CreateLogger<Deployment>()
                };
                var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);

                Logger.LogDebug("Installing plugins");
                foreach (var plugin in RequiredPlugins)
                {
                    var pluginArgs = plugin.Split(" ");
                    await stack.Workspace.InstallPluginAsync(pluginArgs[0], pluginArgs[1]);
                }

                Logger.LogDebug("Setting config");
                var config = Config.Environment.ToTokens("Dungeon:Environment")
                    .Where(entry => !Regex.IsMatch(entry.Key, @"Eks\.Oidc|Iam\.Policies")) // filter values determined at runtime
                    .ToDictionary(entry => entry.Key, entry => new ConfigValue(entry.Value.ToValueString(), Regex.IsMatch(entry.Key, @"Password|Secret")));
                await stack.SetAllConfigAsync(config);

                if (settings.Destroy)
                {
                    if (!settings.NonInteractive &&
                        AnsiConsole.Confirm("[red]Destroy stack?[/]", false) &&
                        AnsiConsole.Prompt(new TextPrompt<string>($@"[red]Confirm destroy stack[/] [blue]""{stackFullName}""[/]").AllowEmpty()) == stackFullName)
                    {
                        Logger.LogDebug("Destroying stack");
                        var result = await stack.DestroyAsync(new DestroyOptions { OnStandardOutput = AnsiConsole.WriteLine });
                        Logger.LogDebug($"Destroyed stack ({result.Summary.Result})");
                    }
                    else
                    {
                        Logger.LogWarning($"Destroy stack skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                    }
                    continue;
                }

                if (settings.Repair)
                {
                    if (!settings.NonInteractive)
                    {
                        Logger.LogDebug("Repairing stack");
                        var exportState = await stack.ExportStackAsync();
                        var json = await RepairStackAsync(exportState.Json.GetRawText());
                        var importState = StackDeployment.FromJsonString(json);
                        var jsonPath = JsonPath.Parse("$.deployment.pending_operations[*].resource.urn").Evaluate(importState.Json);
                        if (importState.Json.IsEquivalentTo(exportState.Json))
                        {
                            Logger.LogWarning("Repaired stack ignored (equivalent)");
                        }
                        else if (jsonPath.Error != null)
                        {
                            Logger.LogWarning($"Repaired stack ignored (error): {jsonPath.Error}");
                        }
                        else if (jsonPath.Matches is { Count: > 0 })
                        {
                            Logger.LogWarning("Repaired stack ignored (pending resources):");
                            foreach (var match in jsonPath.Matches)
                            {
                                Logger.LogWarning(match.Value.ToString());
                            }
                        }
                        else
                        {
                            await stack.ImportStackAsync(importState);
                            Logger.LogDebug("Repaired stack");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Repair stack skipped (non-interactive)");
                    }
                    continue;
                }

                if (settings.Refresh)
                {
                    Logger.LogDebug("Refreshing stack");
                    await stack.RefreshAsync(new RefreshOptions { OnStandardOutput = AnsiConsole.WriteLine });
                }

                if (!settings.SkipPreview)
                {
                    Logger.LogDebug("Previewing stack");
                    var result = await stack.PreviewAsync(
                        new PreviewOptions
                        {
                            Diff = settings.Diff,
                            ExpectNoChanges = settings.ExpectNoChanges,
                            OnStandardOutput = AnsiConsole.WriteLine
                        });
                    if (result.ChangeSummary.All(entry => entry.Key == OperationType.Same))
                    {
                        Logger.LogDebug("Update stack skipped (unchanged)");
                        continue;
                    }
                }
                else
                {
                    Logger.LogDebug("Preview stack skipped");
                }

                if (settings.Approve || !settings.NonInteractive && AnsiConsole.Confirm("[yellow]Update stack?[/]", false))
                {
                    Logger.LogDebug("Updating stack");
                    var result = await stack.UpAsync(
                        new UpOptions
                        {
                            Diff = settings.Diff,
                            ExpectNoChanges = settings.ExpectNoChanges,
                            OnStandardOutput = AnsiConsole.WriteLine
                        });
                    Logger.LogDebug($"Updated stack ({result.Summary.Result})");
                    if (result.Summary.Result != UpdateState.Succeeded)
                    {
                        return -1;
                    }
                }
                else
                {
                    Logger.LogDebug($"Update stack skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                    break;
                }
            }
            return 0;
        }

        private async Task<string> RepairStackAsync(string json)
        {
            var tempFile = Path.GetTempFileName().Replace(".tmp", ".json");
            try
            {
                await File.WriteAllTextAsync(tempFile, json);
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Config.Commands.Deploy.Repair[0],
                    Arguments = string.Join(" ", Config.Commands.Deploy.Repair[1..].Append(tempFile)),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                });
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start interactive repair.");
                }
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Interactive repair returned non-zero exit code: {process.ExitCode}.");
                }
                return await File.ReadAllTextAsync(tempFile);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private IServiceProvider ServiceProvider { get; }
        private Dictionary<Resources, ResourceInfo> ResourceInfo { get; }
        private string[] RequiredPlugins { get; }
    }
}
