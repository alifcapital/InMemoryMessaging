namespace InMemoryMessaging.Exceptions;

/// <summary>
/// The exception which is thrown when one or more handlers of a published message are failed.
/// Its <see cref="AggregateException.InnerExceptions"/> holds the exception of each failed handler, in the order the
/// handlers were executed. Its message carries the id of the stored message, when the retry keeps it.
/// </summary>
/// <param name="message">The text of the exception.</param>
/// <param name="innerExceptions">The exception of each failed handler.</param>
public sealed class InMemoryMessagePublishException(string message, IEnumerable<Exception> innerExceptions)
    : AggregateException(message, innerExceptions);
