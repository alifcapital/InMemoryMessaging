using System.Diagnostics;

namespace InMemoryMessaging.Instrumentation.Trace;

/// <summary>
/// The EventBus instrumentation to create a trace activity 
/// </summary>
internal struct InMemoryMessagingTraceInstrumentation
{
    /// <summary>
    /// The instrumentation name
    /// </summary>
    internal const string InstrumentationName = "InMemoryMessaging";
    
    /// <summary>
    /// Determines whether the instrumentation is enabled or not.
    /// </summary>
    public static bool IsEnabled { get; internal set; }

    /// <summary>
    /// The activity source to create a new activity
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    /// <summary>
    /// For creating activity and use it to add a span
    /// </summary>
    /// <param name="name">Name of new activity</param>
    /// <param name="kind">Type of new activity. The default is <see cref="ActivityKind.Internal"/></param>
    /// <returns>Newly created an open telemetry activity</returns>
    internal static Activity StartActivity(string name, ActivityKind kind = ActivityKind.Producer)
    {
        if (!IsEnabled) return null;

        var traceParentId = Activity.Current?.Id;
        ActivityContext.TryParse(traceParentId, null, out ActivityContext parentContext);
        var activity = ActivitySource.StartActivity(name, kind, parentContext);

        return activity;
    }
}