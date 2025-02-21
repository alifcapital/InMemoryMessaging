using InMemoryMessaging.Instrumentation.Trace;
using OpenTelemetry.Trace;

namespace InMemoryMessaging.Instrumentation;

public static class InstrumentationBuilderExtensions
{
    /// <summary>
    /// Enables OpenTelemetry instrumentation for in-memory messaging. 
    /// </summary>
    /// <param name="builder"><see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> being configured.</param>
    /// <returns>The instance of <see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> to chain the calls.</returns>
    public static TracerProviderBuilder AddInMemoryMessagingInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(InMemoryMessagingTraceInstrumentation.InstrumentationName);
        
        return builder;
    }
}