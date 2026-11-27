using System.Text;
using Yaapic.Core.Variables;

namespace Yaapic.Core.Mocks;

/// <summary>Renders the <c>{{...}}</c> placeholders of a mock response.</summary>
public static class MockTemplate
{
    private static readonly DynamicVariableSource Dynamic = new();

    /// <summary>Replaces the request, parameter and dynamic placeholders of a template.</summary>
    /// <param name="template">The template text.</param>
    /// <param name="request">The request being answered.</param>
    /// <param name="parameters">The path parameters captured by the route.</param>
    /// <returns>The rendered text; unknown tokens stay as written.</returns>
    public static string Render(string template, MockHttpRequest request, IReadOnlyDictionary<string, string> parameters)
    {
        var spans = VariableScanner.ScanSpans(template);
        if (spans.Count == 0)
        {
            return template;
        }

        var sb = new StringBuilder();
        var pos = 0;
        foreach (var span in spans)
        {
            sb.Append(template, pos, span.Start - pos);
            var value = Resolve(span.Name, request, parameters);
            sb.Append(value ?? template.Substring(span.Start, span.Length));
            pos = span.End;
        }

        sb.Append(template, pos, template.Length - pos);
        return sb.ToString();
    }

    private static string? Resolve(string name, MockHttpRequest request, IReadOnlyDictionary<string, string> parameters)
    {
        if (name == "body")
        {
            return Encoding.UTF8.GetString(request.Body);
        }

        if (name.StartsWith("params.", StringComparison.Ordinal))
        {
            return parameters.GetValueOrDefault(name[7..], "");
        }

        if (name.StartsWith("query.", StringComparison.Ordinal))
        {
            var key = name[6..];
            return request.QueryValues.FirstOrDefault(q => q.Key == key)?.Value ?? "";
        }

        if (name.StartsWith("headers.", StringComparison.Ordinal))
        {
            return request.Header(name[8..]) ?? "";
        }

        return Dynamic.TryGet(name, out var v) ? v : null;
    }
}
