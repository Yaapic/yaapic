using System.Diagnostics;
using System.Text;
using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class GoCodeGeneratorTests
{
    private const string Tail =
        "\n\tres, err := http.DefaultClient.Do(req)\n\tif err != nil {\n\t\tpanic(err)\n\t}\n\tdefer res.Body.Close()\n\n"
        + "\tdata, err := io.ReadAll(res.Body)\n\tif err != nil {\n\t\tpanic(err)\n\t}\n"
        + "\tfmt.Println(res.StatusCode, string(data))\n}\n";

    private const string Check = "\tif err != nil {\n\t\tpanic(err)\n\t}\n";

    private static readonly GoCodeGenerator Gen = new();

    private static CodeRequest[] AllSamples =>
    [
        CodeSamples.GetSimple, CodeSamples.PostJson, CodeSamples.PostForm, CodeSamples.PostMultipart, CodeSamples.PutFile,
        CodeSamples.HeadersAwkward, CodeSamples.Unresolved, CodeSamples.DigestNote,
    ];

    private static string Head(params string[] imports) =>
        "package main\n\nimport (\n" + string.Concat(imports.Select(i => $"\t\"{i}\"\n")) + ")\n\nfunc main() {\n";

    [Fact]
    public void Identity()
    {
        Assert.Equal("go-nethttp", Gen.Id);
        Assert.Equal("Go (net/http)", Gen.DisplayName);
        Assert.Equal("go", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        Head("fmt", "io", "net/http")
        + "\treq, err := http.NewRequest(\"GET\", \"https://api.example.com/users?id=1&name=a%20b\", nil)\n" + Check
        + "\treq.Header.Add(\"Accept\", \"application/json\")\n" + Tail,
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        Head("fmt", "io", "net/http", "strings")
        + "\tbody := strings.NewReader(\"{\\\"text\\\":\\\"say \\\\\\\"hi\\\\\\\" C:\\\\\\\\dir\\\\nline2 é\\\"}\")\n"
        + "\treq, err := http.NewRequest(\"POST\", \"https://api.example.com/users\", body)\n" + Check
        + "\treq.Header.Add(\"Accept\", \"application/json\")\n\treq.Header.Add(\"Content-Type\", \"application/json\")\n" + Tail,
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void PostForm()
    {
        var text = Gen.Generate(CodeSamples.PostForm);
        Assert.StartsWith(
            Head("fmt", "io", "net/http", "net/url", "strings")
            + "\tform := url.Values{}\n\tform.Add(\"a\", \"1\")\n\tform.Add(\"b\", \"x y\")\n"
            + "\tbody := strings.NewReader(form.Encode())\n"
            + "\treq, err := http.NewRequest(\"POST\", \"https://api.example.com/form\", body)\n" + Check,
            text);
        Assert.Contains("\treq.Header.Add(\"Content-Type\", \"application/x-www-form-urlencoded\")\n", text);
    }

    [Fact]
    public void PostMultipart() => Assert.Equal(
        Head("bytes", "fmt", "io", "mime/multipart", "net/http", "os")
        + "\tvar buf bytes.Buffer\n\twriter := multipart.NewWriter(&buf)\n"
        + "\tif err := writer.WriteField(\"title\", \"Report\"); err != nil {\n\t\tpanic(err)\n\t}\n"
        + "\tfile1, err := os.Open(\"/tmp/report.pdf\")\n" + Check
        + "\tdefer file1.Close()\n"
        + "\tpart1, err := writer.CreateFormFile(\"file\", \"report.pdf\")\n" + Check
        + "\tif _, err := io.Copy(part1, file1); err != nil {\n\t\tpanic(err)\n\t}\n"
        + "\tif err := writer.Close(); err != nil {\n\t\tpanic(err)\n\t}\n"
        + "\treq, err := http.NewRequest(\"POST\", \"https://api.example.com/upload\", &buf)\n" + Check
        + "\treq.Header.Set(\"Content-Type\", writer.FormDataContentType())\n" + Tail,
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        Head("fmt", "io", "net/http", "os")
        + "\tf, err := os.Open(\"/tmp/data.bin\")\n" + Check + "\tdefer f.Close()\n"
        + "\treq, err := http.NewRequest(\"PUT\", \"https://api.example.com/blob\", f)\n" + Check
        + "\treq.Header.Add(\"Content-Type\", \"application/octet-stream\")\n" + Tail,
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Contains(
        "\treq.Header.Add(\"X-Quote\", \"say \\\"hi\\\" it's\")\n", Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal(
        Head("fmt", "io", "net/http")
        + "\treq, err := http.NewRequest(\"GET\", \"https://{{host}}/users\", nil)\n" + Check + Tail,
        Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.StartsWith(
        "// Digest authentication must be computed by the caller (RFC 7616).\npackage main\n", Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Duplicate_headers_stay_separate() => Assert.Contains(
        "\treq.Header.Add(\"Accept\", \"a\")\n\treq.Header.Add(\"Accept\", \"b\")\n",
        Gen.Generate(new CodeRequest("GET", "https://x.test/", [new("Accept", "a"), new("Accept", "b")], new CodeBody.None(), null)));

    [Fact]
    public void Multipart_drops_content_type() => Assert.DoesNotContain("multipart/form-data\"", Gen.Generate(new CodeRequest(
        "POST", "https://x.test/", [new("Content-Type", "multipart/form-data")], new CodeBody.Multipart([new("a", "1", null)]), null)));

    [Fact]
    public void Imports_are_exactly_those_used()
    {
        Assert.DoesNotContain("\"strings\"", Gen.Generate(CodeSamples.GetSimple));
        Assert.DoesNotContain("\"os\"", Gen.Generate(CodeSamples.PostJson));
        Assert.DoesNotContain("\"os\"", Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [], new CodeBody.Multipart([new("a", "1", null)]), null)));
    }

    [Theory]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\nb", "\"a\\nb\"")]
    [InlineData("a\rb", "\"a\\rb\"")]
    [InlineData("a\tb", "\"a\\tb\"")]
    [InlineData("a\u0001b", "\"a\\x01b\"")]
    [InlineData("a\u007fb", "\"a\\x7fb\"")]
    [InlineData("é€", "\"é€\"")]
    public void Escape_handles_special_characters(string input, string expected) =>
        Assert.Equal(expected, GoCodeGenerator.Escape(input));

    [Fact]
    public async Task Generated_code_is_gofmt_stable()
    {
        var gofmt = FindOnPath("gofmt");
        if (gofmt is null)
        {
            Assert.Skip("gofmt is not on the PATH.");
        }

        foreach (var sample in AllSamples)
        {
            var text = Gen.Generate(sample);
            using var p = Process.Start(new ProcessStartInfo(gofmt, ["-e"])
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
            })!;
            await p.StandardInput.WriteAsync(text);
            p.StandardInput.Close();
            var output = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var err = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await p.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(p.ExitCode == 0, err);
            Assert.Equal(text, output);
        }
    }

    private static string? FindOnPath(string name)
    {
        string[] names = OperatingSystem.IsWindows() ? [name + ".exe"] : [name];
        foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            foreach (var n in names)
            {
                var p = Path.Combine(d, n);
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        return null;
    }
}
