using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Yaapic.Localization;

namespace Yaapic.Core.Import;

/// <summary>Reads and writes <see cref="DataNode"/> trees as JSON.</summary>
public static class DataNodeJson
{
    private const int MaxDepth = 64;

    /// <summary>Parses JSON text (comments and trailing commas accepted, depth limited to 64).</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The tree; numbers keep their source text.</returns>
    /// <exception cref="FormatException">The text is not valid JSON.</exception>
    public static DataNode Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = MaxDepth,
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            return Convert(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new FormatException(Strings.ImportInvalidJson(ex.Message), ex);
        }
    }

    /// <summary>Writes a tree as JSON, without a trailing newline.</summary>
    /// <param name="node">The tree.</param>
    /// <param name="indented">Whether to indent with two spaces.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(DataNode node, bool indented = true)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = indented,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            WriteNode(writer, node);
        }

        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    /// <summary>Builds an Object node.</summary>
    /// <param name="members">The members in order.</param>
    /// <returns>The node.</returns>
    public static DataNode Obj(params (string Key, DataNode Value)[] members) =>
        DataNode.Object([.. members.Select(m => new KeyValuePair<string, DataNode>(m.Key, m.Value))]);

    /// <summary>Builds an Array node.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The node.</returns>
    public static DataNode Arr(params DataNode[] items) => DataNode.Array(items);

    /// <summary>Builds a String node.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The node.</returns>
    public static DataNode Str(string value) => DataNode.String(value);

    /// <summary>Builds a Number node.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The node.</returns>
    public static DataNode Num(long value) => DataNode.Number(value.ToString(CultureInfo.InvariantCulture));

    // Recursion is bounded by the parser's MaxDepth.
    private static DataNode Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => DataNode.Object([.. element.EnumerateObject().Select(p => new KeyValuePair<string, DataNode>(p.Name, Convert(p.Value)))]),
        JsonValueKind.Array => DataNode.Array([.. element.EnumerateArray().Select(Convert)]),
        JsonValueKind.String => DataNode.String(element.GetString()!),
        JsonValueKind.Number => DataNode.Number(element.GetRawText()),
        JsonValueKind.True => DataNode.Bool(true),
        JsonValueKind.False => DataNode.Bool(false),
        _ => DataNode.Null,
    };

    private static void WriteNode(Utf8JsonWriter writer, DataNode node)
    {
        switch (node.Kind)
        {
            case DataKind.Object:
                writer.WriteStartObject();
                foreach (var (key, value) in node.Members)
                {
                    writer.WritePropertyName(key);
                    WriteNode(writer, value);
                }

                writer.WriteEndObject();
                break;
            case DataKind.Array:
                writer.WriteStartArray();
                foreach (var item in node.Items)
                {
                    WriteNode(writer, item);
                }

                writer.WriteEndArray();
                break;
            case DataKind.Number:
                WriteNumber(writer, node.AsString()!);
                break;
            case DataKind.String:
                writer.WriteStringValue(node.AsString());
                break;
            case DataKind.Bool:
                writer.WriteBooleanValue(node.AsBool()!.Value);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }

    // JsonElement.WriteTo keeps the number text and, unlike WriteRawValue, keeps the indentation right.
    private static void WriteNumber(Utf8JsonWriter writer, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Number)
            {
                document.RootElement.WriteTo(writer);
                return;
            }
        }
        catch (JsonException)
        {
            // Not valid JSON number syntax: written as a string below.
        }

        writer.WriteStringValue(text);
    }
}
