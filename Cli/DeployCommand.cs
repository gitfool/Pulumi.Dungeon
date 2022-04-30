namespace Pulumi.Dungeon;

public sealed partial class DeployCommand : AsyncCommandBase<DeployCommand.Settings>
{
    public DeployCommand(IOptions<Config> options, ILoggerFactory loggerFactory, ILogger<DeployCommand> logger, IServiceProvider serviceProvider)
        : base(options, loggerFactory, logger)
    {
        ServiceProvider = serviceProvider;

        StackInfo = new Dictionary<Stacks, StackInfo>
        {
            [Stacks.AwsBootstrap] = new()
            {
                ProjectName = "aws-bootstrap",
                StackType = typeof(BootstrapStack)
            },
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
            "aws v5.3.0",
            "kubernetes v3.18.3",
            "random v4.5.0",
            "tls v4.3.0"
        };
    }

    protected override async Task<int> OnExecuteAsync(CommandContext context, Settings settings)
    {
        Logger.LogInformation("Deploying stacks");
        using var totalTimeLogger = new ElapsedTimeLogger(Logger, "Deployed stacks");

        var infos = settings.Stacks.InOrder(settings.Destroy || settings.Remove)
            .Select(stack => StackInfo[stack])
            .Where(info => info.Environments == null || info.Environments.Contains(Config.Environment.Name)).ToArray();

        foreach (var info in infos)
        {
            var stackFullName = $"{Config.Pulumi.Organization.Name}/{info.ProjectName}/{Config.Environment.Name}";
            Logger.LogInformation($"Deploying {stackFullName}");
            using var stackTimeLogger = new ElapsedTimeLogger(Logger, $"Deployed {stackFullName}");

            var stackName = $"{Config.Pulumi.Organization.Name}/{Config.Environment.Name}";
            var stackArgs = new InlineProgramArgs(info.ProjectName, stackName, PulumiFn.Create(ServiceProvider, info.StackType))
            {
                Logger = LoggerFactory.CreateLogger<Deployment>()
            };
            var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);

            Logger.LogDebug("Installing plugins");
            foreach (var plugin in RequiredPlugins)
            {
                var args = plugin.Split(' ', 2);
                await stack.Workspace.InstallPluginAsync(args[0], args[1]);
            }

            Logger.LogDebug("Setting config");
            var config = Config.Environment.ToTokens("Dungeon:Environment")
                .ToDictionary(entry => entry.Key, entry => new ConfigValue(entry.Value.ToValueString(), Regex.IsMatch(entry.Key, @"Password|Secret|Token")));
            await stack.SetAllConfigAsync(config);

            async Task UnprotectResources()
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

            if (settings.Destroy)
            {
                if (!settings.NonInteractive &&
                    AnsiConsole.Confirm("[red]Destroy stack resources?[/]", false) &&
                    AnsiConsole.Prompt(new TextPrompt<string>($@"[red]Confirm destroy stack resources[/] [blue]""{stackFullName}""[/]:").AllowEmpty()) == stackFullName)
                {
                    Logger.LogDebug("Destroying stack resources");
                    await UnprotectResources();
                    var result = await stack.DestroyAsync(
                        new DestroyOptions
                        {
                            Color = Config.Pulumi.Color,
                            Target = settings.Target?.ToList(),
                            TargetDependents = settings.TargetDependents,
                            OnEvent = @event => OnEvent(settings, @event),
                            OnStandardOutput = stdout => OnStandardOutput(settings, stdout),
                            OnStandardError = stderr => OnStandardError(settings, stderr)
                        });
                    Logger.LogDebug($"Destroyed stack resources ({result.Summary.Result})");
                    if (result.Summary.Result != UpdateState.Succeeded)
                    {
                        return -1;
                    }
                    continue;
                }
                Logger.LogDebug($"Destroy stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                break;
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
                    continue;
                }
                Logger.LogDebug($"Remove stack skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                break;
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
                    continue;
                }
                Logger.LogDebug($"Repair stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                break;
            }

            if (settings.Refresh)
            {
                if (settings.Approve || !settings.NonInteractive && AnsiConsole.Confirm("[yellow]Refresh stack resources?[/]", false))
                {
                    Logger.LogDebug("Refreshing stack resources");
                    var result = await stack.RefreshAsync(
                        new RefreshOptions
                        {
                            Color = Config.Pulumi.Color,
                            Target = settings.Target?.ToList(),
                            OnEvent = @event => OnEvent(settings, @event),
                            OnStandardOutput = stdout => OnStandardOutput(settings, stdout),
                            OnStandardError = stderr => OnStandardError(settings, stderr)
                        });
                    Logger.LogDebug($"Refreshed stack resources ({result.Summary.Result})");
                    if (result.Summary.Result != UpdateState.Succeeded)
                    {
                        return -1;
                    }
                    continue;
                }
                Logger.LogDebug($"Refresh stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                break;
            }

            if (settings.Unprotect)
            {
                if (!settings.NonInteractive && AnsiConsole.Confirm("[yellow]Unprotect stack resources?[/]", false))
                {
                    await UnprotectResources();
                    continue;
                }
                Logger.LogDebug($"Unprotect stack resources skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
                break;
            }

            if (!settings.SkipPreview)
            {
                Logger.LogDebug("Previewing stack");
                var result = await stack.PreviewAsync(
                    new PreviewOptions
                    {
                        Color = Config.Pulumi.Color,
                        Diff = settings.Diff,
                        ExpectNoChanges = settings.ExpectNoChanges,
                        Target = settings.Target?.ToList(),
                        TargetDependents = settings.TargetDependents,
                        OnEvent = @event => OnEvent(settings, @event),
                        OnStandardOutput = stdout => OnStandardOutput(settings, stdout),
                        OnStandardError = stderr => OnStandardError(settings, stderr)
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
                        Color = Config.Pulumi.Color,
                        Diff = settings.Diff,
                        ExpectNoChanges = settings.ExpectNoChanges,
                        Target = settings.Target?.ToList(),
                        TargetDependents = settings.TargetDependents,
                        OnEvent = @event => OnEvent(settings, @event),
                        OnStandardOutput = stdout => OnStandardOutput(settings, stdout),
                        OnStandardError = stderr => OnStandardError(settings, stderr)
                    });
                Logger.LogDebug($"Updated stack ({result.Summary.Result})");
                if (result.Summary.Result != UpdateState.Succeeded)
                {
                    return -1;
                }
                continue;
            }
            Logger.LogDebug($"Update stack skipped ({(settings.NonInteractive ? "non-interactive; " : "")}unapproved)");
            break;
        }
        return 0;
    }

    private static void OnEvent(Settings settings, EngineEvent @event)
    {
        if (!settings.LogEvents)
        {
            return;
        }
        if (@event.CancelEvent != null)
        {
            Console.WriteLine(new { @event.CancelEvent }.ToJson(false));
        }
        else if (@event.StandardOutputEvent != null)
        {
            Console.WriteLine(new { @event.StandardOutputEvent }.ToJson(false));
        }
        else if (@event.DiagnosticEvent != null)
        {
            Console.WriteLine(new { @event.DiagnosticEvent }.ToJson(false));
        }
        else if (@event.PreludeEvent != null)
        {
            Console.WriteLine(new { @event.PreludeEvent }.ToJson(false));
        }
        else if (@event.SummaryEvent != null)
        {
            Console.WriteLine(new { @event.SummaryEvent }.ToJson(false));
        }
        else if (@event.ResourcePreEvent != null)
        {
            Console.WriteLine(new { @event.ResourcePreEvent }.ToJson(false));
        }
        else if (@event.ResourceOutputsEvent != null)
        {
            Console.WriteLine(new { @event.ResourceOutputsEvent }.ToJson(false));
        }
        else if (@event.ResourceOperationFailedEvent != null)
        {
            Console.WriteLine(new { @event.ResourceOperationFailedEvent }.ToJson(false));
        }
        else if (@event.PolicyEvent != null)
        {
            Console.WriteLine(@event.PolicyEvent.ToJson(false));
        }
        else
        {
            Console.WriteLine(@event.ToJson(false));
        }
    }

    private static void OnStandardOutput(Settings settings, string stdout)
    {
        if (!settings.LogEvents)
        {
            Console.Out.WriteLine(stdout);
        }
    }

    private static void OnStandardError(Settings settings, string stderr)
    {
        if (!settings.LogEvents)
        {
            Console.Error.WriteLine(stderr);
        }
    }

    private async Task<string> RepairStackAsync(string json)
    {
        var tempFile = Path.GetTempFileName().Replace(".tmp", ".json");
        try
        {
            await File.WriteAllTextAsync(tempFile, json);
            var args = Config.Commands.Deploy.Repair.Split(' ', 2);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = args[0],
                Arguments = string.Join(' ', args[1..].Append(tempFile)),
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
