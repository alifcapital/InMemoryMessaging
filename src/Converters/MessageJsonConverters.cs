using System.Text.Json;
using System.Text.Json.Serialization;

namespace InMemoryMessaging.Converters;

/// <summary>
/// The options of writing the payload of a message and reading it back.
/// </summary>
internal static class MessageJsonConverters
{
    /// <summary>
    /// The same options are used for writing and reading, so a payload written by one version of the application is
    /// readable by the next one.
    /// </summary>
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
