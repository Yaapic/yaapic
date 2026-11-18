using System.Globalization;
using System.Text;

namespace Yaapic.Core.CodeGeneration;

/// <summary>Generates a runnable Go <c>main</c> program using <c>net/http</c>.</summary>
public sealed class GoCodeGenerator : ICodeGenerator
{
    /// <inheritdoc/>
    public string Id => "go-nethttp";

    /// <inheritdoc/>
    public string DisplayName => "Go (net/http)";

    /// <inheritdoc/>
    public string EditorLanguage => "go";

    /// <summary>Writes text as an interpreted Go string literal on one line.</summary>
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
        var imports = new SortedSet<string>(StringComparer.Ordinal) { "fmt", "io", "net/http" };
        var setup = new List<string>();
        var bodyArg = "nil";
        var multipart = request.Body is CodeBody.Multipart;

        switch (request.Body)
        {
            case CodeBody.Text t:
                imports.Add("strings");
                setup.Add($"\tbody := strings.NewReader({Escape(t.Content)})");
                bodyArg = "body";
                break;
            case CodeBody.Form f:
                imports.Add("strings");
                imports.Add("net/url");
                setup.Add("\tform := url.Values{}");
                setup.AddRange(f.Fields.Select(x => $"\tform.Add({Escape(x.Key)}, {Escape(x.Value)})"));
                setup.Add("\tbody := strings.NewReader(form.Encode())");
                bodyArg = "body";
                break;
            case CodeBody.Multipart m:
                imports.Add("bytes");
                imports.Add("mime/multipart");
                setup.Add("\tvar buf bytes.Buffer");
                setup.Add("\twriter := multipart.NewWriter(&buf)");
                for (var i = 0; i < m.Parts.Count; i++)
                {
                    var p = m.Parts[i];
                    if (p.FilePath is null)
                    {
                        setup.AddRange(Check($"err := writer.WriteField({Escape(p.Name)}, {Escape(p.Value ?? string.Empty)})", inline: true));
                        continue;
                    }

                    imports.Add("os");
                    setup.AddRange(Check($"file{i}, err := os.Open({Escape(p.FilePath)})"));
                    setup.Add($"\tdefer file{i}.Close()");
                    setup.AddRange(Check($"part{i}, err := writer.CreateFormFile({Escape(p.Name)}, {Escape(Path.GetFileName(p.FilePath))})"));
                    setup.AddRange(Check($"_, err := io.Copy(part{i}, file{i})", inline: true));
                }

                setup.AddRange(Check("err := writer.Close()", inline: true));
                bodyArg = "&buf";
                break;
            case CodeBody.File file:
                imports.Add("os");
                setup.AddRange(Check($"f, err := os.Open({Escape(file.Path)})"));
                setup.Add("\tdefer f.Close()");
                bodyArg = "f";
                break;
        }

        var lines = new List<string>();
        if (request.AuthNote is not null)
        {
            lines.Add($"// {request.AuthNote}");
        }

        lines.Add("package main");
        lines.Add(string.Empty);
        lines.Add("import (");
        lines.AddRange(imports.Select(i => $"\t\"{i}\""));
        lines.Add(")");
        lines.Add(string.Empty);
        lines.Add("func main() {");
        lines.AddRange(setup);
        lines.AddRange(Check($"req, err := http.NewRequest({Escape(request.Method)}, {Escape(request.Url)}, {bodyArg})"));
        foreach (var h in request.Headers.Where(h => !(multipart && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))))
        {
            lines.Add($"\treq.Header.Add({Escape(h.Key)}, {Escape(h.Value)})");
        }

        if (multipart)
        {
            lines.Add("\treq.Header.Set(\"Content-Type\", writer.FormDataContentType())");
        }

        lines.Add(string.Empty);
        lines.AddRange(Check("res, err := http.DefaultClient.Do(req)"));
        lines.Add("\tdefer res.Body.Close()");
        lines.Add(string.Empty);
        lines.AddRange(Check("data, err := io.ReadAll(res.Body)"));
        lines.Add("\tfmt.Println(res.StatusCode, string(data))");
        lines.Add("}");
        return CodeText.JoinLines(lines);
    }

    // A statement followed by its panic-on-error check; inline puts the statement in the if header.
    private static string[] Check(string statement, bool inline = false) => inline
        ? [$"\tif {statement}; err != nil {{", "\t\tpanic(err)", "\t}"]
        : [$"\t{statement}", "\tif err != nil {", "\t\tpanic(err)", "\t}"];
}
