using System.Text.Json;
using InMemoryMessaging.Models;
using InMemoryMessaging.Converters;

namespace InMemoryMessaging.Services;

/// <summary>
/// Serializes a message to the JSON payload to store it, and deserializes the payload back to retry its handlers.
/// A payload which cannot be serialized or deserialized is reported as a reason rather than thrown.
/// </summary>
internal static class MessageJsonSerializer
{
    /// <summary>
    /// Serializes the message to JSON by its runtime type, so the properties of the concrete type are kept.
    /// </summary>
    /// <returns>Returns false when the message cannot be serialized, and the reason in the <paramref name="error"/>.</returns>
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
            error = $"Could not serialize the '{messageType.FullName}' message to JSON: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Deserializes the JSON payload back to the given type of the message.
    /// </summary>
    /// <returns>Returns false when the payload cannot be deserialized, and the reason in the <paramref name="error"/>.</returns>
    internal static bool TryDeserialize(string payload, Type messageType, out IMessage message, out string error)
    {
        message = null;
        error = null;

        try
        {
            message = (IMessage)JsonSerializer.Deserialize(payload, messageType, MessageJsonConverters.Options);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not deserialize the JSON payload of the '{messageType.FullName}' message: {exception.Message}";
            return false;
        }
    }
}
