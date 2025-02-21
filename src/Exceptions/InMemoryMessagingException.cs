namespace InMemoryMessaging.Exceptions;

internal class InMemoryMessagingException : Exception
{
    public InMemoryMessagingException(string message) : base(message)
    {
    }

    public InMemoryMessagingException(Exception innerException, string message) : base(message, innerException)
    {
    }
}