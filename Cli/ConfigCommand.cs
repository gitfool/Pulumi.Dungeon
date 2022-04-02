using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Pulumi.Dungeon;

public sealed partial class ConfigCommand : CommandBase<ConfigCommand.Settings>
{
    public ConfigCommand(IOptions<Config> options, ILoggerFactory loggerFactory, ILogger<ConfigCommand> logger)
        : base(options, loggerFactory, logger) { }

    protected override int OnExecute(CommandContext context, Settings settings)
    {
        if (settings.Yaml)
        {
            static void Configure(SerializerBuilder builder) => builder
                .WithAttributeOverride<AwsConfig>(config => config.AccountId, new YamlMemberAttribute { ScalarStyle = ScalarStyle.DoubleQuoted })
                .WithTypeInspector(inner => new ConfigTypeInspector(inner));

            var yaml = Config.ToYaml(Configure);
            AnsiConsole.WriteLine(yaml);
            return 0;
        }

        var table = new Table { Border = TableBorder.None, ShowHeaders = false }
            .AddColumn("Token", column => column.PadRight(4))
            .AddColumn("Value");
        var config = Config.ToTokens()
            .ToDictionary(entry => entry.Key.EscapeMarkup(), entry => entry.Value.ToValueString().EscapeMarkup());
        foreach (var (token, value) in config)
        {
            table.AddRow(token, value);
        }
        AnsiConsole.Write(table);

        var result = Config.Validate();
        if (!result.IsValid)
        {
            var errors = result.ToString().EscapeMarkup();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]{errors}[/]");
            return -1;
        }
        return 0;
    }
}
