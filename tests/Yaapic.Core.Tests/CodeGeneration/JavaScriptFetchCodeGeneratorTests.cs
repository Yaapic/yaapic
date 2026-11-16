using System.Diagnostics;
using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class JavaScriptFetchCodeGeneratorTests
{
    private static readonly JavaScriptFetchCodeGenerator Gen = new();

    private static CodeRequest[] AllSamples =>
    [
        CodeSamples.GetSimple, CodeSamples.PostJson, CodeSamples.PostForm, CodeSamples.PostMultipart, CodeSamples.PutFile,
        CodeSamples.HeadersAwkward, CodeSamples.Unresolved, CodeSamples.DigestNote,
    ];

    [Fact]
    public void Identity()
    {
        Assert.Equal("javascript-fetch", Gen.Id);
        Assert.Equal("JavaScript (fetch)", Gen.DisplayName);
        Assert.Equal("javascript", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        "const response = await fetch(\"https://api.example.com/users?id=1&name=a%20b\", {\n"
        + "  headers: {\n    \"Accept\": \"application/json\"\n  }\n});\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        "const response = await fetch(\"https://api.example.com/users\", {\n"
        + "  method: \"POST\",\n"
        + "  headers: {\n    \"Accept\": \"application/json\",\n    \"Content-Type\": \"application/json\"\n  },\n"
        + "  body: \"{\\\"text\\\":\\\"say \\\\\\\"hi\\\\\\\" C:\\\\\\\\dir\\\\nline2 é\\\"}\"\n});\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void PostForm() => Assert.Equal(
        "const response = await fetch(\"https://api.example.com/form\", {\n"
        + "  method: \"POST\",\n"
        + "  body: new URLSearchParams({ \"a\": \"1\", \"b\": \"x y\" })\n});\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.PostForm));

    [Fact]
    public void PostMultipart() => Assert.Equal(
        "import { openAsBlob } from \"node:fs\";\n\n"
        + "const form = new FormData();\nform.append(\"title\", \"Report\");\n"
        + "form.append(\"file\", await openAsBlob(\"/tmp/report.pdf\"));\n\n"
        + "const response = await fetch(\"https://api.example.com/upload\", {\n"
        + "  method: \"POST\",\n  body: form\n});\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        "import { openAsBlob } from \"node:fs\"; // Node.js 20+\n\n"
        + "const response = await fetch(\"https://api.example.com/blob\", {\n"
        + "  method: \"PUT\",\n"
        + "  headers: {\n    \"Content-Type\": \"application/octet-stream\"\n  },\n"
        + "  body: await openAsBlob(\"/tmp/data.bin\")\n});\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Contains(
        "    \"X-Quote\": \"say \\\"hi\\\" it's\"\n", Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal(
        "const response = await fetch(\"https://{{host}}/users\");\n"
        + "const data = await response.text();\nconsole.log(response.status, data);\n",
        Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.StartsWith(
        "// Digest authentication must be computed by the caller (RFC 7616).\nconst response", Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Duplicate_headers_are_merged()
    {
        var text = Gen.Generate(new CodeRequest(
            "GET", "https://x.test/", [new("Accept", "a"), new("accept", "b"), new("X", "1")], new CodeBody.None(), null));
        Assert.Contains("    \"Accept\": \"a, b\",\n    \"X\": \"1\"\n", text);
    }

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
    [InlineData("a\u0001b", "\"a\\u0001b\"")]
    [InlineData("a\u2028b\u2029", "\"a\\u2028b\\u2029\"")]
    [InlineData("é€", "\"é€\"")]
    public void Escape_handles_special_characters(string input, string expected) =>
        Assert.Equal(expected, JavaScriptFetchCodeGenerator.Escape(input));

    [Fact]
    public async Task Generated_code_is_valid_javascript()
    {
        var node = FindOnPath("node");
        if (node is null)
        {
            Assert.Skip("node is not on the PATH.");
        }

        var dir = Directory.CreateTempSubdirectory("yaapic-js");
        try
        {
            for (var i = 0; i < AllSamples.Length; i++)
            {
                var lines = CodeText.Lines(Gen.Generate(AllSamples[i]));
                var imports = string.Join('\n', lines.Where(l => l.StartsWith("import ", StringComparison.Ordinal)));
                var body = string.Join('\n', lines.Where(l => !l.StartsWith("import ", StringComparison.Ordinal)));

                // Imports must stay at module top level, so the file is an .mjs with the rest wrapped in an async IIFE.
                var file = Path.Combine(dir.FullName, $"s{i}.mjs");
                await File.WriteAllTextAsync(file, $"{imports}\n(async () => {{\n{body}\n}})();\n", TestContext.Current.CancellationToken);
                using var p = Process.Start(new ProcessStartInfo(node, ["--check", file])
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                })!;
                var err = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                await p.WaitForExitAsync(TestContext.Current.CancellationToken);
                Assert.True(p.ExitCode == 0, $"sample {i}: {err}");
            }
        }
        finally
        {
            dir.Delete(true);
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
