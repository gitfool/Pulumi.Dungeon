using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public sealed partial class ConfigCommand : CommandBase<ConfigCommand.Settings>
    {
        public ConfigCommand(IOptions<Config> options, ILoggerFactory loggerFactory, ILogger<ConfigCommand> logger)
            : base(options, loggerFactory, logger) { }

        protected override int OnExecute(CommandContext context, Settings settings)
        {
            var table = new Table { Border = TableBorder.None, ShowHeaders = false }
                .AddColumn("Token", column => column.PadRight(4))
                .AddColumn("Value");
            var config = Config.ToTokens()
                .Where(entry => !Regex.IsMatch(entry.Key, @"Eks\.Oidc|Iam\.Policies")) // filter values determined at runtime
                .ToDictionary(entry => entry.Key.EscapeMarkup(), entry => entry.Value.ToValueString().EscapeMarkup());
            foreach (var (token, value) in config)
            {
                table.AddRow(token, value);
            }
            AnsiConsole.Render(table);
            return 0;
        }
    }
}
