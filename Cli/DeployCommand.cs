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

            StackInfo = new Dictionary<Stacks, StackInfo>
            {
                [Stacks.AwsVpc] = new()
                {
                    ProjectName = "aws-vpc",
                    StackType = typeof(VpcStack)
                },
                [Stacks.AwsEks] = new()
                {
                    ProjectName = "aws-eks",
                    StackType = typeof(EksStack)
                },
                [Stacks.K8s] = new()
                {
                    ProjectName = "k8s",
                    StackType = typeof(K8sStack)
                }
            };

            // renovate: datasource=github-releases
            RequiredPlugins = new[]
            {
                "aws v4.29.0",
                "kubernetes v3.10.1",
                "random v4.3.1",
                "tls v4.0.0"
            };
        }

        protected override async Task<int> OnExecuteAsync(CommandContext context, Settings settings)
        {
            Logger.LogInformation("Deploying stacks");
            using var totalTimeLogger = new ElapsedTimeLogger(Logger, "Deployed stacks");

            var infos = settings.Stacks.InOrder(settings.Destroy || settings.Remove)
                .Select(stack => StackInfo[stack])
                .Where(info => info.Environments == null || info.Environments.Contains(settings.Environment, StringComparer.OrdinalIgnoreCase)).ToArray();

            foreach (var info in infos)
            {
                var stackFullName = $"{Config.Pulumi.Organization.Name}/{info.ProjectName}/{settings.Environment.ToLower()}";
                Logger.LogInformation($"Deploying {stackFullName}");
                using var stackTimeLogger = new ElapsedTimeLogger(Logger, $"Deployed {stackFullName}");

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
                    .ToDictionary(entry => entry.Key, entry => new ConfigValue(entry.Value.ToValueString(), Regex.IsMatch(entry.Key, @"Password|Secret|Token")));
                await stack.SetAllConfigAsync(config);

                if (settings.Destroy)
                {
                    if (!settings.NonInteractive &&
                        AnsiConsole.Confirm("[red]Destroy stack resources?[/]", false) &&
                        AnsiConsole.Prompt(new TextPrompt<string>($@"[red]Confirm destroy stack resources[/] [blue]""{stackFullName}""[/]:").AllowEmpty()) == stackFullName)
                    {
                        if (settings.Unprotect)
                        {
                            Logger.LogDebug("Unprotecting stack resources");
                            if (settings.Target != null)
                            {
                                foreach (var target in settings.Target)
                                {
                                    await stack.State.UnprotectAsync(target);
                                }
                            }
                            else
                            {
                                await stack.State.UnprotectAllAsync();
                            }
                            Logger.LogDebug("Unprotected stack resources");
                        }

                        Logger.LogDebug("Destroying stack resources");
                        var result = await stack.DestroyAsync(
                            new DestroyOptions
                            {
                                Target = settings.Target?.ToList(),
                                TargetDependents = settings.TargetDependents,
                                OnStandardOutput = AnsiConsole.WriteLine
                            });
                        Logger.LogDebug($"Destroyed stack resources ({result.Summary.Result})");
                    }
                    else
                    {
                        Logger.LogWarning($"Destroy stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                    }
                    continue;
                }

                if (settings.Remove)
                {
                    if (!settings.NonInteractive &&
                        AnsiConsole.Confirm("[red]Remove stack?[/]", false) &&
                        AnsiConsole.Prompt(new TextPrompt<string>($@"[red]Confirm remove stack[/] [blue]""{stackFullName}""[/]:").AllowEmpty()) == stackFullName)
                    {
                        Logger.LogDebug("Removing stack");
                        await stack.Workspace.RemoveStackAsync(stackFullName);
                        Logger.LogDebug("Removed stack");
                    }
                    else
                    {
                        Logger.LogWarning($"Remove stack skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                    }
                    continue;
                }

                if (settings.Repair)
                {
                    if (!settings.NonInteractive && AnsiConsole.Confirm("[red]Repair stack resources?[/]", false))
                    {
                        Logger.LogDebug("Repairing stack resources");
                        var exportState = await stack.ExportStackAsync();
                        var json = await RepairStackAsync(exportState.Json.GetRawText());
                        var importState = StackDeployment.FromJsonString(json);
                        var jsonPath = JsonPath.Parse("$.deployment.pending_operations[*].resource.urn").Evaluate(importState.Json);
                        if (importState.Json.IsEquivalentTo(exportState.Json))
                        {
                            Logger.LogWarning("Repaired stack resources ignored (equivalent)");
                        }
                        else if (jsonPath.Error != null)
                        {
                            Logger.LogWarning($"Repaired stack resources ignored (error): {jsonPath.Error}");
                        }
                        else if (jsonPath.Matches is { Count: > 0 })
                        {
                            Logger.LogWarning("Repaired stack resources ignored (pending resources):");
                            foreach (var match in jsonPath.Matches)
                            {
                                Logger.LogWarning(match.Value.ToString());
                            }
                        }
                        else
                        {
                            await stack.ImportStackAsync(importState);
                            Logger.LogDebug("Repaired stack resources");
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"Repair stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                    }
                    continue;
                }

                if (settings.Refresh)
                {
                    if (settings.Approve || !settings.NonInteractive && AnsiConsole.Confirm("[yellow]Refresh stack resources?[/]", false))
                    {
                        Logger.LogDebug("Refreshing stack resources");
                        var result = await stack.RefreshAsync(
                            new RefreshOptions
                            {
                                Target = settings.Target?.ToList(),
                                OnStandardOutput = AnsiConsole.WriteLine
                            });
                        Logger.LogDebug($"Refreshed stack resources ({result.Summary.Result})");
                        if (result.Summary.Result != UpdateState.Succeeded)
                        {
                            return -1;
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"Refresh stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                        break;
                    }
                    continue;
                }

                if (!settings.SkipPreview)
                {
                    Logger.LogDebug("Previewing stack");
                    var result = await stack.PreviewAsync(
                        new PreviewOptions
                        {
                            Diff = settings.Diff,
                            ExpectNoChanges = settings.ExpectNoChanges,
                            Target = settings.Target?.ToList(),
                            TargetDependents = settings.TargetDependents,
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
                            Target = settings.Target?.ToList(),
                            TargetDependents = settings.TargetDependents,
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
        private Dictionary<Stacks, StackInfo> StackInfo { get; }
        private string[] RequiredPlugins { get; }
    }
}
