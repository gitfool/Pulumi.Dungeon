using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Pulumi.Dungeon
{
    public abstract class AsyncCommandBase<TSettings> : AsyncCommand<TSettings> where TSettings : CommandSettings
    {
        protected AsyncCommandBase(IOptions<Config> options, ILoggerFactory loggerFactory, ILogger logger)
        {
            Config = options.Value;
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
        {
            try
            {
                return await OnExecuteAsync(context, settings);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
                return -1;
            }
        }

        protected abstract Task<int> OnExecuteAsync(CommandContext context, TSettings settings);

        protected Config Config { get; }
        protected ILoggerFactory LoggerFactory { get; }
        protected ILogger Logger { get; }
    }
}
