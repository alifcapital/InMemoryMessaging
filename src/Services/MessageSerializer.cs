using System.Text.Json;
using InMemoryMessaging.Models;
using InMemoryMessaging.Converters;

namespace InMemoryMessaging.Services;

/// <summary>
/// Writes the payload of a message to store it, and reads it back to retry its handlers.
/// A payload which cannot be written or read is reported as a reason rather than thrown.
/// </summary>
internal static class MessageSerializer
{
    /// <summary>
    /// Writes the message by its runtime type, so the properties of the concrete type are kept.
    /// </summary>
    /// <returns>Returns false when the message cannot be written, and the reason in the <paramref name="error"/>.</returns>
    internal static bool TrySerialize(IMessage message, out string payload, out string error)
    {
        payload = null;
        error = null;

        var messageType = message.GetType();

        try
        {
            payload = JsonSerializer.Serialize(message, messageType, MessageJsonConverters.Options);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not write the payload of the '{messageType.FullName}' message: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reads the payload back as the given type.
    /// </summary>
    /// <returns>Returns false when the payload cannot be read, and the reason in the <paramref name="error"/>.</returns>
    internal static bool TryDeserialize(string payload, Type messageType, out IMessage message, out string error)
    {
        message = null;
        error = null;

        try
        {
            message = JsonSerializer.Deserialize(payload, messageType, MessageJsonConverters.Options) as IMessage;
            if (message is not null)
                return true;

            error = $"The payload of the '{messageType.FullName}' message was read as null.";
            return false;
        }
        catch (Exception exception)
        {
            error = $"Could not read the payload of the '{messageType.FullName}' message: {exception.Message}";
            return false;
        }
    }
}
