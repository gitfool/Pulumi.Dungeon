using System.Linq;
using Serilog.Core;
using Serilog.Events;

namespace Pulumi.Dungeon
{
    public sealed class SourceContextUqnEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
        {
            if (evt.Properties.TryGetValue("SourceContext", out var sourceContextProperty) &&
                sourceContextProperty is ScalarValue { Value: string sourceContext })
            {
                var sourceContextUqn = sourceContext.Split('.').LastOrDefault();
                evt.AddOrUpdateProperty(new LogEventProperty("SourceContextUqn", new ScalarValue(sourceContextUqn)));
            }
        }
    }
}
