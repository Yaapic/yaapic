namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a <c>curl</c> command for bash and zsh.</summary>
public sealed class CurlCodeGenerator : ICodeGenerator
{
    /// <inheritdoc/>
    public string Id => "curl";

    /// <inheritdoc/>
    public string DisplayName => "cURL";

    /// <inheritdoc/>
    public string EditorLanguage => "shell";

    /// <inheritdoc/>
    public string Generate(CodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parts = new List<string>();
        var url = Quote(request.Url);
        parts.Add(request.Method switch
        {
            "GET" => $"curl {url}",
            "HEAD" => $"curl --head {url}",
            _ => $"curl --request {request.Method} {url}",
        });

        foreach (var h in request.Headers)
        {
            parts.Add($"--header {Quote($"{h.Key}: {h.Value}")}");
        }

        switch (request.Body)
        {
            case CodeBody.Text t:
                parts.Add($"--data-raw {Quote(t.Content)}");
                break;
            case CodeBody.Form f:
                parts.AddRange(f.Fields.Select(x => $"--data-urlencode {Quote($"{x.Key}={x.Value}")}"));
                break;
            case CodeBody.Multipart m:
                parts.AddRange(m.Parts.Select(Part));
                break;
            case CodeBody.File file:
                parts.Add($"--data-binary {Quote($"@{file.Path}")}");
                break;
        }

        var text = string.Join(" \\\n  ", parts) + "\n";
        return request.AuthNote is null ? text : $"# {request.AuthNote}\n{text}";
    }

    private static string Part(CodePart p)
    {
        if (p.FilePath is not null)
        {
            return $"--form {Quote($"{p.Name}=@{p.FilePath}")}";
        }

        var value = p.Value ?? string.Empty;
        var option = value.StartsWith('@') || value.StartsWith('<') ? "--form-string" : "--form";
        return $"{option} {Quote($"{p.Name}={value}")}";
    }

    private static string Quote(string s) => "'" + s.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
