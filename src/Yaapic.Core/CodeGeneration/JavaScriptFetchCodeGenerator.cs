using System.Globalization;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a <c>fetch</c> snippet for browsers and Node.js 18+.</summary>
public sealed class JavaScriptFetchCodeGenerator : ICodeGenerator
{
    private const string FormType = "application/x-www-form-urlencoded";

    /// <inheritdoc/>
    public string Id => "javascript-fetch";

    /// <inheritdoc/>
    public string DisplayName => "JavaScript (fetch)";

    /// <inheritdoc/>
    public string EditorLanguage => "javascript";

    /// <summary>Writes text as a double-quoted JavaScript string literal.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The literal, quotes included.</returns>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case (char)0x2028 or (char)0x2029 or < ' ' or (char)0x7f:
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.Append('"').ToString();
    }

    /// <inheritdoc/>
    public string Generate(CodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = new List<string>();
        if (request.AuthNote is not null)
        {
            lines.Add($"// {request.AuthNote}");
        }

        var multipart = request.Body as CodeBody.Multipart;
        if (request.Body is CodeBody.File)
        {
            lines.Add("import { openAsBlob } from \"node:fs\"; // Node.js 20+");
            lines.Add(string.Empty);
        }
        else if (multipart is not null && multipart.Parts.Any(p => p.FilePath is not null))
        {
            lines.Add("import { openAsBlob } from \"node:fs\";");
            lines.Add(string.Empty);
        }

        if (multipart is not null)
        {
            lines.Add("const form = new FormData();");
            foreach (var p in multipart.Parts)
            {
                var value = p.FilePath is not null ? $"await openAsBlob({Escape(p.FilePath)})" : Escape(p.Value ?? string.Empty);
                lines.Add($"form.append({Escape(p.Name)}, {value});");
            }

            lines.Add(string.Empty);
        }

        var options = Options(request);
        if (options.Count == 0)
        {
            lines.Add($"const response = await fetch({Escape(request.Url)});");
        }
        else
        {
            lines.Add($"const response = await fetch({Escape(request.Url)}, {{");
            for (var i = 0; i < options.Count; i++)
            {
                var last = options[i].Count - 1;
                var comma = i < options.Count - 1 ? "," : string.Empty;
                lines.AddRange(options[i].Select((l, j) => "  " + l + (j == last ? comma : string.Empty)));
            }

            lines.Add("});");
        }

        lines.Add("const data = await response.text();");
        lines.Add("console.log(response.status, data);");
        return CodeText.JoinLines(lines);
    }

    private static List<List<string>> Options(CodeRequest request)
    {
        var options = new List<List<string>>();
        if (request.Method != "GET")
        {
            options.Add([$"method: {Escape(request.Method)}"]);
        }

        var headers = request.Headers
            .Where(h => !DropsContentType(request.Body, h))
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (headers.Count > 0)
        {
            var block = new List<string> { "headers: {" };
            block.AddRange(headers.Select((g, i) =>
                $"  {Escape(g.Key)}: {Escape(string.Join(", ", g.Select(h => h.Value)))}" + (i < headers.Count - 1 ? "," : string.Empty)));
            block.Add("}");
            options.Add(block);
        }

        switch (request.Body)
        {
            case CodeBody.Text t:
                options.Add([$"body: {Escape(t.Content)}"]);
                break;
            case CodeBody.Form f:
                options.Add([f.Fields.Count == 0
                    ? "body: new URLSearchParams()"
                    : $"body: new URLSearchParams({{ {string.Join(", ", f.Fields.Select(x => $"{Escape(x.Key)}: {Escape(x.Value)}"))} }})"]);
                break;
            case CodeBody.Multipart:
                options.Add(["body: form"]);
                break;
            case CodeBody.File file:
                options.Add([$"body: await openAsBlob({Escape(file.Path)})"]);
                break;
        }

        return options;
    }

    private static bool DropsContentType(CodeBody body, KeyValueEntry h) =>
        h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
        && (body is CodeBody.Multipart
            || (body is CodeBody.Form && h.Value.Split(';')[0].Trim().Equals(FormType, StringComparison.OrdinalIgnoreCase)));
}
