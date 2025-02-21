using InMemoryMessaging.Instrumentation.Trace;
using OpenTelemetry.Trace;

namespace InMemoryMessaging.Instrumentation;

public static class InstrumentationBuilderExtensions
{
    /// <summary>
    /// Enables the incoming and outgoing events automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> to chain the calls.</returns>
    public static TracerProviderBuilder AddEventBusInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(InMemoryMessagingTraceInstrumentation.InstrumentationName);
        
        return builder;
    }
}