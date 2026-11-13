using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>One failing assertion written in both languages and the message both engines must produce.</summary>
/// <param name="Javascript">The JavaScript statements run inside one <c>yaapic.test</c>.</param>
/// <param name="Lua">The Lua statements run inside one <c>yaapic.test</c>.</param>
/// <param name="Message">The recorded failure message, identical in both languages.</param>
internal sealed record ExpectMessageCase(string Javascript, string Lua, string Message);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ExpectMessageCase[]))]
internal sealed partial class ExpectMessagesContext : JsonSerializerContext;

/// <summary>Theory data read from <c>ExpectMessages.json</c>, shared by the JavaScript and Lua assertion tests.</summary>
public static class ExpectMessages
{
    private static ExpectMessageCase[] Load() =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Conformance", "ExpectMessages.json")),
            ExpectMessagesContext.Default.ExpectMessageCaseArray)
        ?? [];

    /// <summary>Gets the JavaScript statements and their expected message.</summary>
    /// <returns>The theory data.</returns>
    public static TheoryData<string, string> JavaScript()
    {
        TheoryData<string, string> data = [];
        foreach (var c in Load())
        {
            data.Add(c.Javascript, c.Message);
        }

        return data;
    }

    /// <summary>Gets the Lua statements and their expected message.</summary>
    /// <returns>The theory data.</returns>
    public static TheoryData<string, string> Lua()
    {
        TheoryData<string, string> data = [];
        foreach (var c in Load())
        {
            data.Add(c.Lua, c.Message);
        }

        return data;
    }
}
