using System.Globalization;
using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Generates a deterministic sample value from an OpenAPI schema.</summary>
/// <param name="refs">The reference resolver of the document.</param>
/// <param name="report">Where the truncation notice is reported.</param>
internal sealed class OpenApiSchemaSampler(OpenApiRefResolver refs, ImportReport report)
{
    private const int MaxDepth = 6;
    private const int MaxNodes = 2000;
    private const int MaxPadding = 256;

    private readonly HashSet<DataNode> _expanding = new(ReferenceEqualityComparer.Instance);
    private int _nodes;

    /// <summary>Builds a sample value for a schema.</summary>
    /// <param name="schema">The schema node (a <c>$ref</c> is followed).</param>
    /// <param name="path">The report path of the schema.</param>
    /// <returns>The sample, or <see cref="DataNode.Null"/> when nothing can be generated.</returns>
    public DataNode Sample(DataNode schema, string path)
    {
        _nodes = 0;
        return Build(schema, path, 0);
    }

    private static string StringSample(DataNode schema)
    {
        var text = schema.GetString("format") switch
        {
            "date-time" => "2024-01-01T00:00:00Z",
            "date" => "2024-01-01",
            "time" => "00:00:00",
            "uuid" => "00000000-0000-0000-0000-000000000000",
            "email" => "user@example.com",
            "uri" or "url" => "https://example.com",
            "hostname" => "example.com",
            "ipv4" => "192.0.2.1",
            "ipv6" => "2001:db8::1",
            "byte" or "binary" => "",
            "password" => "password",
            _ => "string",
        };

        return text == "string" && schema["minLength"]?.AsDouble() is > 6 and var min
            ? "string".PadRight((int)Math.Min(min, MaxPadding), 'x')
            : text;
    }

    private static string? TypeOf(DataNode schema)
    {
        var type = schema["type"];
        if (type is { IsArray: true })
        {
            return type.Items.Select(i => i.AsString()).FirstOrDefault(t => t is not null and not "null");
        }

        return type?.AsString() ?? (schema["properties"] is not null ? "object" : schema["items"] is not null ? "array" : null);
    }

    private DataNode Build(DataNode? raw, string path, int depth)
    {
        if (raw is null)
        {
            return DataNode.Null;
        }

        var schema = refs.Resolve(raw, path);
        if (!schema.IsObject)
        {
            return DataNode.Null;
        }

        if (depth > MaxDepth || ++_nodes > MaxNodes || !_expanding.Add(schema))
        {
            report.AddOnce(ImportSeverity.Info, path, Strings.ImportOpenapiSampleTruncated);
            return DataNode.Null;
        }

        try
        {
            return Expand(schema, path, depth);
        }
        finally
        {
            _expanding.Remove(schema);
        }
    }

    private DataNode Expand(DataNode schema, string path, int depth)
    {
        if ((schema["example"] ?? schema["default"] ?? (schema["enum"] is { IsArray: true } values ? values[0] : null) ?? schema["const"]) is { } fixedValue)
        {
            return fixedValue;
        }

        if (schema["allOf"] is { IsArray: true } allOf)
        {
            return Merge(allOf, path, depth);
        }

        if ((schema["oneOf"] ?? schema["anyOf"]) is { IsArray: true } choices)
        {
            return Build(choices[0], path, depth);
        }

        return TypeOf(schema) switch
        {
            "object" => SampleObject(schema, path, depth),
            "array" => DataNode.Array(schema["items"] is { } items ? [Build(items, path, depth + 1)] : []),
            "string" => DataNode.String(StringSample(schema)),
            "integer" or "number" => DataNode.Number(schema["minimum"]?.AsString() ?? "0"),
            "boolean" => DataNode.Bool(false),
            _ => DataNode.Null,
        };
    }

    private DataNode Merge(DataNode allOf, string path, int depth)
    {
        List<KeyValuePair<string, DataNode>> merged = [];
        foreach (var member in allOf.Items)
        {
            if (Build(member, path, depth) is not { IsObject: true } part)
            {
                continue;
            }

            foreach (var pair in part.Members)
            {
                var at = merged.FindIndex(m => m.Key == pair.Key);
                if (at >= 0)
                {
                    merged[at] = pair;
                }
                else
                {
                    merged.Add(pair);
                }
            }
        }

        return DataNode.Object(merged);
    }

    private DataNode SampleObject(DataNode schema, string path, int depth)
    {
        List<KeyValuePair<string, DataNode>> members = [];
        if (schema["properties"] is { IsObject: true } properties)
        {
            foreach (var (name, property) in properties.Members)
            {
                var resolved = refs.Resolve(property, $"{path}/{name}");
                if (resolved.GetBool("readOnly") == true)
                {
                    continue;
                }

                members.Add(new(name, Build(resolved, $"{path}/{name}", depth + 1)));
            }
        }

        return DataNode.Object(members);
    }
}
