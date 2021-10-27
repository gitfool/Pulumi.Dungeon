using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Pulumi.Dungeon
{
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

                var yaml = new { Config.Environment }.ToYaml(Configure);
                AnsiConsole.WriteLine(yaml);
            }
            else
            {
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
            }
            return 0;
        }
    }
}
