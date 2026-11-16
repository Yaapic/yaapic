using System.Diagnostics;
using System.Text;
using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class PythonRequestsCodeGeneratorTests
{
    private const string Tail = "print(response.status_code, response.text)\n";

    private static readonly PythonRequestsCodeGenerator Gen = new();

    private static CodeRequest[] AllSamples =>
    [
        CodeSamples.GetSimple, CodeSamples.PostJson, CodeSamples.PostForm, CodeSamples.PostMultipart, CodeSamples.PutFile,
        CodeSamples.HeadersAwkward, CodeSamples.Unresolved, CodeSamples.DigestNote,
    ];

    [Fact]
    public void Identity()
    {
        Assert.Equal("python-requests", Gen.Id);
        Assert.Equal("Python (requests)", Gen.DisplayName);
        Assert.Equal("python", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        "import requests\n\nurl = \"https://api.example.com/users?id=1&name=a%20b\"\n"
        + "headers = {\n    \"Accept\": \"application/json\",\n}\n\n"
        + "response = requests.request(\"GET\", url, headers=headers)\n" + Tail,
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        "import requests\n\nurl = \"https://api.example.com/users\"\n"
        + "headers = {\n    \"Accept\": \"application/json\",\n    \"Content-Type\": \"application/json\",\n}\n"
        + "payload = \"{\\\"text\\\":\\\"say \\\\\\\"hi\\\\\\\" C:\\\\\\\\dir\\\\nline2 é\\\"}\"\n\n"
        + "response = requests.request(\"POST\", url, headers=headers, data=payload.encode(\"utf-8\"))\n" + Tail,
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void PostForm() => Assert.Equal(
        "import requests\n\nurl = \"https://api.example.com/form\"\n\n"
        + "response = requests.request(\"POST\", url, data={\"a\": \"1\", \"b\": \"x y\"})\n" + Tail,
        Gen.Generate(CodeSamples.PostForm));

    [Fact]
    public void PostMultipart() => Assert.Equal(
        "import requests\n\nurl = \"https://api.example.com/upload\"\n\n"
        + "response = requests.request(\"POST\", url, files=[(\"title\", (None, \"Report\")), "
        + "(\"file\", (\"report.pdf\", open(\"/tmp/report.pdf\", \"rb\")))])\n" + Tail,
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        "import requests\n\nurl = \"https://api.example.com/blob\"\n"
        + "headers = {\n    \"Content-Type\": \"application/octet-stream\",\n}\n\n"
        + "response = requests.request(\"PUT\", url, headers=headers, data=open(\"/tmp/data.bin\", \"rb\"))\n" + Tail,
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Contains(
        "    \"X-Quote\": \"say \\\"hi\\\" it's\",\n", Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal(
        "import requests\n\nurl = \"https://{{host}}/users\"\n\n"
        + "response = requests.request(\"GET\", url)\n" + Tail,
        Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.StartsWith(
        "# Digest authentication must be computed by the caller (RFC 7616).\nimport requests\n", Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Duplicate_headers_are_merged()
    {
        var text = Gen.Generate(new CodeRequest(
            "GET", "https://x.test/", [new("Accept", "a"), new("accept", "b"), new("X", "1")], new CodeBody.None(), null));
        Assert.Contains("    \"Accept\": \"a, b\",\n    \"X\": \"1\",\n", text);
    }

    [Fact]
    public void Repeated_form_field_becomes_a_list() => Assert.Contains(
        "data={\"a\": [\"1\", \"2\"], \"b\": \"3\"}",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Form([new("a", "1"), new("b", "3"), new("a", "2")]), null)));

    [Fact]
    public void Form_with_explicit_content_type_drops_it_but_other_types_stay()
    {
        var form = new CodeBody.Form([new("a", "1")]);
        Assert.DoesNotContain("headers", Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [new("content-type", "application/x-www-form-urlencoded")], form, null)));
        Assert.Contains("headers", Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [new("Content-Type", "text/plain")], form, null)));
    }

    [Fact]
    public void Multipart_drops_content_type() => Assert.DoesNotContain("headers", Gen.Generate(new CodeRequest(
        "POST", "https://x.test/", [new("Content-Type", "multipart/form-data")], new CodeBody.Multipart([new("a", "1", null)]), null)));

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
        Assert.Equal(expected, PythonRequestsCodeGenerator.Escape(input));

    [Fact]
    public async Task Generated_code_is_valid_python()
    {
        var python = FindOnPath("python");
        if (python is null)
        {
            Assert.Skip("python is not on the PATH.");
        }

        foreach (var sample in AllSamples)
        {
            using var p = Process.Start(new ProcessStartInfo(python, ["-c", "import ast,sys; ast.parse(sys.stdin.buffer.read().decode('utf-8'))"])
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
            })!;
            await p.StandardInput.WriteAsync(Gen.Generate(sample));
            p.StandardInput.Close();
            var err = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await p.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(p.ExitCode == 0, err);
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
