using System.Globalization;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a Python 3 script using the <c>requests</c> package.</summary>
public sealed class PythonRequestsCodeGenerator : ICodeGenerator
{
    private const string FormType = "application/x-www-form-urlencoded";

    /// <inheritdoc/>
    public string Id => "python-requests";

    /// <inheritdoc/>
    public string DisplayName => "Python (requests)";

    /// <inheritdoc/>
    public string EditorLanguage => "python";

    /// <summary>Writes text as a double-quoted Python string literal on one line.</summary>
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
                case < ' ' or (char)0x7f:
                    sb.Append("\\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
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
            lines.Add($"# {request.AuthNote}");
        }

        lines.Add("import requests");
        lines.Add(string.Empty);
        lines.Add($"url = {Escape(request.Url)}");

        var headers = request.Headers
            .Where(h => !DropsContentType(request.Body, h))
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var args = new List<string> { Escape(request.Method), "url" };
        if (headers.Count > 0)
        {
            lines.Add("headers = {");
            lines.AddRange(headers.Select(g => $"    {Escape(g.Key)}: {Escape(string.Join(", ", g.Select(h => h.Value)))},"));
            lines.Add("}");
            args.Add("headers=headers");
        }

        switch (request.Body)
        {
            case CodeBody.Text t:
                lines.Add($"payload = {Escape(t.Content)}");
                args.Add("data=payload.encode(\"utf-8\")");
                break;
            case CodeBody.Form f:
                var fields = f.Fields.GroupBy(x => x.Key).Select(g => $"{Escape(g.Key)}: {FormValue(g.Select(x => x.Value).ToList())}");
                args.Add($"data={{{string.Join(", ", fields)}}}");
                break;
            case CodeBody.Multipart m:
                args.Add($"files=[{string.Join(", ", m.Parts.Select(Part))}]");
                break;
            case CodeBody.File file:
                args.Add($"data=open({Escape(file.Path)}, \"rb\")");
                break;
        }

        lines.Add(string.Empty);
        lines.Add($"response = requests.request({string.Join(", ", args)})");
        lines.Add("print(response.status_code, response.text)");
        return CodeText.JoinLines(lines);
    }

    private static string FormValue(List<string> values) =>
        values.Count == 1 ? Escape(values[0]) : $"[{string.Join(", ", values.Select(Escape))}]";

    private static string Part(CodePart p) => p.FilePath is not null
        ? $"({Escape(p.Name)}, ({Escape(Path.GetFileName(p.FilePath))}, open({Escape(p.FilePath)}, \"rb\")))"
        : $"({Escape(p.Name)}, (None, {Escape(p.Value ?? string.Empty)}))";

    private static bool DropsContentType(CodeBody body, KeyValueEntry h) =>
        h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
        && (body is CodeBody.Multipart
            || (body is CodeBody.Form && h.Value.Split(';')[0].Trim().Equals(FormType, StringComparison.OrdinalIgnoreCase)));
}
