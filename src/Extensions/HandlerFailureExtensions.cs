using InMemoryMessaging.Models;

namespace InMemoryMessaging.Extensions;

internal static class HandlerFailureExtensions
{
    /// <summary>
    /// Creates the stored handler of a message from the failure of the handler.
    /// </summary>
    /// <param name="failure">The failure of the handler.</param>
    /// <returns>Returns the handler to keep in the failed message.</returns>
    internal static FailedMessageHandler ToFailedMessageHandler(this HandlerFailure failure)
    {
        return new FailedMessageHandler
        {
            HandlerPath = failure.HandlerPath,
            FailureReason = failure.FailureReason
        };
    }

    /// <summary>
    /// Combines the reasons of all failed handlers into the one reason of the message.
    /// </summary>
    /// <param name="failures">The failures of the handlers.</param>
    /// <param name="maxLength">The maximum length of the reason. The "0" value means no limit.</param>
    /// <returns>Returns the combined reason, cut to the given length.</returns>
    internal static string CombineFailureReasons(this IReadOnlyList<HandlerFailure> failures, int maxLength)
    {
        var combined = string.Join(Environment.NewLine,
            failures.Select(failure => $"{failure.MessageHandlerType.Name}: {failure.FailureReason}"));

        return combined.TruncateFailureReason(maxLength);
    }
}
