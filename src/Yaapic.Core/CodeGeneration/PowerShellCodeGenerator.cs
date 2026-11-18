using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a PowerShell script using <c>Invoke-RestMethod</c> (PowerShell 5.1 and 7).</summary>
public sealed class PowerShellCodeGenerator : ICodeGenerator
{
    private const string FormType = "application/x-www-form-urlencoded";

    /// <inheritdoc/>
    public string Id => "powershell";

    /// <inheritdoc/>
    public string DisplayName => "PowerShell (Invoke-RestMethod)";

    /// <inheritdoc/>
    public string EditorLanguage => "powershell";

    /// <summary>Writes text as a single-quoted PowerShell string literal; quote characters are doubled.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The literal, quotes included.</returns>
    public static string Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length + 2).Append('\'');
        foreach (var c in value)
        {
            sb.Append(c);

            // PowerShell also treats the typographic single quotes as quote characters.
            if (c is '\'' or '‘' or '’' or '‚' or '‛')
            {
                sb.Append(c);
            }
        }

        return sb.Append('\'').ToString();
    }

    /// <summary>Writes text as a double-quoted PowerShell string literal on one line.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The literal, quotes included.</returns>
    public static string DoubleQuote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var sb = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '\n':
                    sb.Append("`n");
                    break;
                case '\r':
                    sb.Append("`r");
                    break;
                case '`' or '"' or '$' or '“' or '”' or '„':
                    sb.Append('`').Append(c);
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

        if (request.Body is CodeBody.Multipart)
        {
            lines.Add("# Requires PowerShell 7+");
        }

        var groups = request.Headers
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var contentType = groups.FirstOrDefault(g => g.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
        var contentTypeValue = contentType is null ? null : string.Join(", ", contentType.Select(h => h.Value));
        if (DropsContentType(request.Body, contentTypeValue))
        {
            contentTypeValue = null;
        }

        var headers = groups.Where(g => g != contentType).ToList();
        var args = new List<string> { $"-Uri {Quote(request.Url)}", $"-Method {MethodName(request.Method)}" };
        if (headers.Count > 0)
        {
            lines.Add("$headers = @{");
            lines.AddRange(headers.Select(g => $"    {DoubleQuote(g.Key)} = {DoubleQuote(string.Join(", ", g.Select(h => h.Value)))}"));
            lines.Add("}");
            args.Add("-Headers $headers");
        }

        var bodyArgs = new List<string>();
        switch (request.Body)
        {
            case CodeBody.Text t:
                lines.Add(t.Content.Contains('\n') || t.Content.Contains('\r') ? HereString(t.Content) : $"$body = {Quote(t.Content)}");
                bodyArgs.Add("-Body $body");
                break;
            case CodeBody.Form f when f.Fields.GroupBy(x => x.Key).Any(g => g.Count() > 1):
                lines.Add("# repeated field names cannot be a hashtable: sent as a url-encoded string");
                lines.Add($"$body = {Quote(string.Join('&', f.Fields.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")))}");
                contentTypeValue ??= FormType;
                bodyArgs.Add("-Body $body");
                break;
            case CodeBody.Form f:
                bodyArgs.Add($"-Body {Table(f.Fields.Select(x => (x.Key, DoubleQuote(x.Value))))}");
                break;
            case CodeBody.Multipart m:
                bodyArgs.Add($"-Form {Table(m.Parts.Select(p => (p.Name, p.FilePath is not null ? $"Get-Item -Path {Quote(p.FilePath)}" : DoubleQuote(p.Value ?? string.Empty))))}");
                break;
            case CodeBody.File file:
                bodyArgs.Add($"-InFile {Quote(file.Path)}");
                break;
        }

        if (contentTypeValue is not null)
        {
            args.Add($"-ContentType {Quote(contentTypeValue)}");
        }

        args.AddRange(bodyArgs);
        lines.Add(string.Empty);
        lines.Add($"$response = Invoke-RestMethod {string.Join(' ', args)}");
        lines.Add("$response");
        return CodeText.JoinLines(lines);
    }

    private static string Table(IEnumerable<(string Key, string Value)> entries) =>
        $"@{{ {string.Join("; ", entries.Select(e => $"{DoubleQuote(e.Key)} = {e.Value}"))} }}";

    private static string HereString(string content)
    {
        var lines = CodeText.Lines(content);

        // A line starting with '@ would end the here-string early: fall back to a multi-line quoted string.
        return lines.Any(l => l.StartsWith("'@", StringComparison.Ordinal))
            ? $"$body = {Quote(string.Join('\n', lines))}"
            : $"$body = @'\n{string.Join('\n', lines)}\n'@";
    }

    private static string MethodName(string method) => method.ToUpperInvariant() switch
    {
        "GET" => "Get",
        "POST" => "Post",
        "PUT" => "Put",
        "DELETE" => "Delete",
        "HEAD" => "Head",
        "OPTIONS" => "Options",
        "PATCH" => "Patch",
        _ => method,
    };

    private static bool DropsContentType(CodeBody body, string? value) =>
        value is not null
        && (body is CodeBody.Multipart
            || (body is CodeBody.Form && value.Split(';')[0].Trim().Equals(FormType, StringComparison.OrdinalIgnoreCase)));
}
