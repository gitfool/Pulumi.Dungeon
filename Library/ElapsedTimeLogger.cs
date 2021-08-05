using System;
using Microsoft.Extensions.Logging;

namespace Pulumi.Dungeon
{
    public sealed class ElapsedTimeLogger : IDisposable
    {
        public ElapsedTimeLogger(ILogger logger, string message, LogLevel logLevel = LogLevel.Information)
        {
            Logger = logger;
            LogLevel = logLevel;
            Message = message;
            StartTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            var elapsedTime = DateTime.UtcNow - StartTime;
            Logger.Log(LogLevel, $@"{Message} in {elapsedTime:hh\:mm\:ss}");
        }

        private ILogger Logger { get; }
        private LogLevel LogLevel { get; }
        private string Message { get; }
        private DateTime StartTime { get; set; }
    }
}
