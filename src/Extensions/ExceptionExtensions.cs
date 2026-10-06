using System.Text;
using InMemoryMessaging.Configurations;

namespace InMemoryMessaging.Extensions;

internal static class ExceptionExtensions
{
    /// <summary>
    /// Builds the reason of a failure from the exception and its inner ones:
    /// "InvalidOperationException: Could not reserve the order ---> NpgsqlException: connection timeout".
    /// </summary>
    /// <param name="exception">The exception of the failure.</param>
    /// <param name="options">The options to know whether to add the stack trace and how long the reason may be.</param>
    /// <returns>Returns the reason of the failure.</returns>
    internal static string ToFailureReason(this Exception exception, InMemoryMessagingRetrySettings options)
    {
        var failureReason = new StringBuilder();
        for (var currentException = exception; currentException is not null; currentException = currentException.InnerException)
        {
            if (failureReason.Length > 0)
                failureReason.Append(" ---> ");

            failureReason.Append(currentException.GetType().Name).Append(": ").Append(currentException.Message);
        }

        if (options.StoreFailureStackTrace && exception.StackTrace is not null)
            failureReason.AppendLine().Append(exception.StackTrace);

        return failureReason.ToString().TruncateFailureReason(options.MaxFailureReasonLength);
    }

    /// <summary>
    /// Cuts the reason of a failure to the given length.
    /// </summary>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="maxLength">The maximum length of the reason. The "0" value means no limit.</param>
    /// <returns>Returns the reason, cut to the given length.</returns>
    internal static string TruncateFailureReason(this string failureReason, int maxLength)
    {
        if (maxLength <= 0 || failureReason.Length <= maxLength)
            return failureReason;

        return failureReason[..maxLength];
    }
}
