using Serilog;

namespace Pulumi.Dungeon;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureAppConfiguration(this IHostBuilder hostBuilder, string[] args) =>
        hostBuilder.ConfigureAppConfiguration((context, builder) =>
        {
            ((List<IConfigurationSource>)builder.Sources).RemoveAll(
                source => source.GetType() == typeof(EnvironmentVariablesConfigurationSource) || source.GetType() == typeof(CommandLineConfigurationSource));

            builder.AddYamlFile("config/_default.yaml", false, false) // global defaults
                .AddYamlFile($"config/_{context.HostingEnvironment.EnvironmentName.ToLowerInvariant()}.yaml", true, false); // dotnet environment; development, production

            if (args.Length >= 2 && args[0] is "config" or "deploy" && !args[1].StartsWith("-")) // command environment; alpha, beta, etc
            {
                foreach (var extension in GetExtensions($"config/{args[1]}.yaml"))
                {
                    builder.AddYamlFile($"config/{extension}.yaml", false, false);
                }
                builder.AddYamlFile($"config/{args[1]}.yaml", false, false);
            }

            builder.AddEnvironmentVariables() // env vars
                .AddCommandLine(args); // cli

            static IEnumerable<string> GetExtensions(string path)
            {
                var header = File.ReadLines(path).First();
                var match = Regex.Match(header, @"^# extends:(?: (?<extension>[^ ]+))+$");
                return match.Success ? match.Groups["extension"].Captures.Select(capture => capture.Value) : Array.Empty<string>();
            }
        });

    public static IHostBuilder ConfigureServices(this IHostBuilder hostBuilder) =>
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.Configure<Config>(context.Configuration.GetSection(Constants.ConfigKey));

            // pulumi stacks must be transient across preview and update!
            services.AddTransient<BootstrapStack>()
                .AddTransient<VpcStack>()
                .AddTransient<EksStack>()
                .AddTransient<K8sStack>();
        });

    public static async Task<int> RunCommandAsync(this IHostBuilder hostBuilder, string[] args)
    {
        try
        {
            var commandApp = new CommandApp(new HostTypeRegistrar(hostBuilder));
            commandApp.Configure(config =>
            {
                config.SetApplicationName(Constants.AppName);
                config.UseAssemblyInformationalVersion();
                config.AddCommand<ConfigCommand>("config");
                config.AddCommand<DeployCommand>("deploy");
                //config.PropagateExceptions();
            });
            return await commandApp.RunAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
            return -1;
        }
    }

    public static IHostBuilder UseSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, config) =>
        {
            config.ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.When(_ => context.HostingEnvironment.IsDevelopment(), enrich => enrich.With<SourceContextUqnEnricher>())
                .Enrich.WithProperty("ApplicationName", Constants.AppName);

            if (context.HostingEnvironment.IsDevelopment())
            {
                var writeToConsole = context.Configuration.GetSection("Serilog:WriteTo").GetChildren()
                    .Any(section => section.GetValue<string>("Name") == "Console");

                if (!writeToConsole)
                {
                    config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContextUqn}) {Message:lj}{NewLine}{Exception}");
                }
            }
            else
            {
                config.WriteTo.Console();
            }
        }, true);
}
