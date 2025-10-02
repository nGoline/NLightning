using Serilog.Core;
using Serilog.Events;

namespace NLightning.Daemon.Helpers;

public class ClassNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContextProperty))
        {
            return;
        }

        // Extract the class name from the fully qualified type name
        var sourceContext = sourceContextProperty.ToString().Trim('"');
        var className = sourceContext.Split('.').LastOrDefault() ?? sourceContext;

        var classNameProperty = propertyFactory.CreateProperty("ClassName", className);
        logEvent.AddPropertyIfAbsent(classNameProperty);
    }
}