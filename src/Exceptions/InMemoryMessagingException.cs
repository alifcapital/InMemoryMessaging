namespace InMemoryMessaging.Exceptions;

/// <summary>
/// The exception of the in-memory messaging, for example when a handler is registered wrongly or the options of the
/// retry are not filled.
/// </summary>
public class InMemoryMessagingException : Exception
{
    public InMemoryMessagingException(string message) : base(message)
    {
    }

    public InMemoryMessagingException(Exception innerException, string message) : base(message, innerException)
    {
    }
}
