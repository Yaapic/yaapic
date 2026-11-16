using System.Diagnostics;
using System.Text;
using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class PowerShellCodeGeneratorTests
{
    private const string Tail = "$response\n";

    private static readonly PowerShellCodeGenerator Gen = new();

    private static CodeRequest[] AllSamples =>
    [
        CodeSamples.GetSimple, CodeSamples.PostJson, CodeSamples.PostForm, CodeSamples.PostMultipart, CodeSamples.PutFile,
        CodeSamples.HeadersAwkward, CodeSamples.Unresolved, CodeSamples.DigestNote,
        new("POST", "https://x.test/", [], new CodeBody.Text("{\n  \"a\": 1\n}", "application/json"), null),
        new("POST", "https://x.test/", [], new CodeBody.Form([new("a", "1"), new("a", "2")]), null),
    ];

    [Fact]
    public void Identity()
    {
        Assert.Equal("powershell", Gen.Id);
        Assert.Equal("PowerShell (Invoke-RestMethod)", Gen.DisplayName);
        Assert.Equal("powershell", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        "$headers = @{\n    \"Accept\" = \"application/json\"\n}\n\n"
        + "$response = Invoke-RestMethod -Uri 'https://api.example.com/users?id=1&name=a%20b' -Method Get -Headers $headers\n" + Tail,
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        "$headers = @{\n    \"Accept\" = \"application/json\"\n}\n"
        + "$body = '{\"text\":\"say \\\"hi\\\" C:\\\\dir\\nline2 é\"}'\n\n"
        + "$response = Invoke-RestMethod -Uri 'https://api.example.com/users' -Method Post -Headers $headers -ContentType 'application/json' -Body $body\n" + Tail,
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void Json_with_newline_uses_a_here_string() => Assert.Equal(
        "$body = @'\n{\n  \"a\": 1\n}\n'@\n\n"
        + "$response = Invoke-RestMethod -Uri 'https://x.test/' -Method Post -Body $body\n" + Tail,
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Text("{\r\n  \"a\": 1\r\n}", null), null)));

    [Fact]
    public void Here_string_terminator_line_falls_back_to_a_quoted_string() => Assert.Contains(
        "$body = 'a\n''@\nb'\n",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Text("a\n'@\nb", null), null)));

    [Fact]
    public void PostForm() => Assert.Equal(
        "\n$response = Invoke-RestMethod -Uri 'https://api.example.com/form' -Method Post -Body @{ \"a\" = \"1\"; \"b\" = \"x y\" }\n" + Tail,
        Gen.Generate(CodeSamples.PostForm));

    [Fact]
    public void PostMultipart() => Assert.Equal(
        "# Requires PowerShell 7+\n\n"
        + "$response = Invoke-RestMethod -Uri 'https://api.example.com/upload' -Method Post "
        + "-Form @{ \"title\" = \"Report\"; \"file\" = Get-Item -Path '/tmp/report.pdf' }\n" + Tail,
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        "\n$response = Invoke-RestMethod -Uri 'https://api.example.com/blob' -Method Put "
        + "-ContentType 'application/octet-stream' -InFile '/tmp/data.bin'\n" + Tail,
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Contains(
        "    \"X-Quote\" = \"say `\"hi`\" it's\"\n", Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal(
        "\n$response = Invoke-RestMethod -Uri 'https://{{host}}/users' -Method Get\n" + Tail,
        Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.StartsWith(
        "# Digest authentication must be computed by the caller (RFC 7616).\n", Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Auth_note_comes_before_the_PowerShell_7_comment() => Assert.StartsWith(
        "# note\n# Requires PowerShell 7+\n",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Multipart([new("a", "1", null)]), "note")));

    [Fact]
    public void Duplicate_headers_are_merged()
    {
        var text = Gen.Generate(new CodeRequest(
            "GET", "https://x.test/", [new("Accept", "a"), new("accept", "b"), new("X", "1")], new CodeBody.None(), null));
        Assert.Contains("    \"Accept\" = \"a, b\"\n    \"X\" = \"1\"\n", text);
    }

    [Fact]
    public void Content_type_goes_to_the_parameter_not_the_headers()
    {
        var text = Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [new("content-type", "text/plain")], new CodeBody.Text("x", null), null));
        Assert.DoesNotContain("$headers", text);
        Assert.Contains("-ContentType 'text/plain' -Body $body", text);
    }

    [Fact]
    public void Repeated_form_field_falls_back_to_an_encoded_string() => Assert.Equal(
        "# repeated field names cannot be a hashtable: sent as a url-encoded string\n$body = 'a=1&b=x%20y&a=2'\n\n"
        + "$response = Invoke-RestMethod -Uri 'https://x.test/' -Method Post -ContentType 'application/x-www-form-urlencoded' -Body $body\n" + Tail,
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Form([new("a", "1"), new("b", "x y"), new("a", "2")]), null)));

    [Fact]
    public void Form_with_other_content_type_keeps_it() => Assert.Contains(
        "-ContentType 'text/plain' -Body @{",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [new("Content-Type", "text/plain")], new CodeBody.Form([new("a", "1")]), null)));

    [Theory]
    [InlineData("GET", "Get")]
    [InlineData("post", "Post")]
    [InlineData("PUT", "Put")]
    [InlineData("DELETE", "Delete")]
    [InlineData("HEAD", "Head")]
    [InlineData("OPTIONS", "Options")]
    [InlineData("PATCH", "Patch")]
    [InlineData("PROPFIND", "PROPFIND")]
    public void Method_casing(string method, string expected) => Assert.Contains(
        $" -Method {expected}", Gen.Generate(new CodeRequest(method, "https://x.test/", [], new CodeBody.None(), null)));

    [Theory]
    [InlineData("a'b", "'a''b'")]
    [InlineData("a\u2019b", "'a\u2019\u2019b'")]
    [InlineData("a\\b", "'a\\b'")]
    public void Quote_doubles_quote_characters(string input, string expected) =>
        Assert.Equal(expected, PowerShellCodeGenerator.Quote(input));

    [Theory]
    [InlineData("a`b", "\"a``b\"")]
    [InlineData("a\"b", "\"a`\"b\"")]
    [InlineData("a$b", "\"a`$b\"")]
    [InlineData("a\nb", "\"a`nb\"")]
    [InlineData("a\rb", "\"a`rb\"")]
    [InlineData("a\u201Cb", "\"a`\u201Cb\"")]
    [InlineData("é€", "\"é€\"")]
    public void DoubleQuote_escapes_special_characters(string input, string expected) =>
        Assert.Equal(expected, PowerShellCodeGenerator.DoubleQuote(input));

    [Fact]
    public async Task Generated_code_is_valid_powershell()
    {
        var shell = FindOnPath("pwsh") ?? FindOnPath("powershell");
        if (shell is null)
        {
            Assert.Skip("Neither pwsh nor powershell is on the PATH.");
        }

        const string Script = "$t=[Console]::In.ReadToEnd();$e=$null;"
            + "[void][System.Management.Automation.Language.Parser]::ParseInput($t,[ref]$null,[ref]$e);"
            + "if($e.Count -gt 0){[Console]::Error.WriteLine(($e|Out-String));exit 1}";
        foreach (var sample in AllSamples)
        {
            using var p = Process.Start(new ProcessStartInfo(shell, ["-NoProfile", "-NonInteractive", "-Command", Script])
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
