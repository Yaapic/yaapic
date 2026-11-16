using System.Globalization;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a top-level-statements C# snippet using <see cref="HttpClient"/>.</summary>
public sealed class CSharpHttpClientCodeGenerator : ICodeGenerator
{
    private const string FormType = "application/x-www-form-urlencoded";
    private const string Indent = "    ";

    /// <inheritdoc/>
    public string Id => "csharp-httpclient";

    /// <inheritdoc/>
    public string DisplayName => "C# (HttpClient)";

    /// <inheritdoc/>
    public string EditorLanguage => "csharp";

    /// <summary>Writes text as a regular C# string literal on one line.</summary>
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
                case '\0':
                    sb.Append("\\0");
                    break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.Append('"').ToString();
    }

    /// <inheritdoc/>
    public string Generate(CodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var hasBody = request.Body is not CodeBody.None;
        bool IsContent(KeyValueEntry h) => hasBody && h.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase);
        var contentType = request.Headers.FirstOrDefault(
            h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && h.Value.Length > 0)?.Value;
        var lines = new List<string>();
        if (request.AuthNote is not null)
        {
            lines.Add($"// {request.AuthNote}");
        }

        if (request.Body is CodeBody.Text)
        {
            lines.Add("using System.Text;");
            lines.Add(string.Empty);
        }

        lines.Add("using var client = new HttpClient();");
        lines.Add($"using var request = new HttpRequestMessage({Method(request.Method)}, {Escape(request.Url)});");
        lines.AddRange(request.Headers.Where(h => !IsContent(h))
            .Select(h => $"request.Headers.TryAddWithoutValidation({Escape(h.Key)}, {Escape(h.Value)});"));

        var contentHeaders = request.Headers.Where(IsContent)
            .Where(h => !h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).ToList();
        switch (request.Body)
        {
            case CodeBody.Text t:
                var type = contentType is null ? string.Empty : $", {Escape(contentType.Split(';')[0].Trim())}";
                lines.Add($"request.Content = new StringContent({Escape(t.Content)}, Encoding.UTF8{type});");
                break;
            case CodeBody.Form f:
                lines.Add($"request.Content = {FormContent(f)};");
                if (contentType is not null && !contentType.Split(';')[0].Trim().Equals(FormType, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add("request.Content.Headers.Remove(\"Content-Type\");");
                    lines.Add($"request.Content.Headers.TryAddWithoutValidation(\"Content-Type\", {Escape(contentType)});");
                }

                break;
            case CodeBody.Multipart m:
                lines.Add("request.Content = new MultipartFormDataContent");
                lines.Add("{");
                lines.AddRange(m.Parts.Select(p => $"{Indent}{Part(p)},"));
                lines.Add("};");
                break;
            case CodeBody.File file:
                lines.Add($"request.Content = new StreamContent(File.OpenRead({Escape(file.Path)}));");
                if ((contentType ?? file.ContentType) is { Length: > 0 } fileType)
                {
                    lines.Add($"request.Content.Headers.TryAddWithoutValidation(\"Content-Type\", {Escape(fileType)});");
                }

                break;
        }

        lines.AddRange(contentHeaders.Select(h => $"request.Content.Headers.TryAddWithoutValidation({Escape(h.Key)}, {Escape(h.Value)});"));
        lines.Add(string.Empty);
        lines.Add("using var response = await client.SendAsync(request);");
        lines.Add("Console.WriteLine((int)response.StatusCode);");
        lines.Add("Console.WriteLine(await response.Content.ReadAsStringAsync());");
        return CodeText.JoinLines(lines);
    }

    private static string Method(string method) => method switch
    {
        "GET" => "HttpMethod.Get",
        "POST" => "HttpMethod.Post",
        "PUT" => "HttpMethod.Put",
        "DELETE" => "HttpMethod.Delete",
        "HEAD" => "HttpMethod.Head",
        "OPTIONS" => "HttpMethod.Options",
        "PATCH" => "HttpMethod.Patch",
        _ => $"new HttpMethod({Escape(method)})",
    };

    private static string FormContent(CodeBody.Form f)
    {
        var repeated = f.Fields.GroupBy(x => x.Key).Any(g => g.Count() > 1);
        return repeated
            ? "new FormUrlEncodedContent(new List<KeyValuePair<string, string>> { "
                + string.Join(", ", f.Fields.Select(x => $"new({Escape(x.Key)}, {Escape(x.Value)})")) + " })"
            : "new FormUrlEncodedContent(new Dictionary<string, string> { "
                + string.Join(", ", f.Fields.Select(x => $"[{Escape(x.Key)}] = {Escape(x.Value)}")) + " })";
    }

    private static string Part(CodePart p) => p.FilePath is not null
        ? $"{{ new StreamContent(File.OpenRead({Escape(p.FilePath)})), {Escape(p.Name)}, {Escape(Path.GetFileName(p.FilePath))} }}"
        : $"{{ new StringContent({Escape(p.Value ?? string.Empty)}), {Escape(p.Name)} }}";
}
