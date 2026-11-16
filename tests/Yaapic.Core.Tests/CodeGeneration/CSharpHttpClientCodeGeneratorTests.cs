using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class CSharpHttpClientCodeGeneratorTests
{
    private const string Tail =
        "\nusing var response = await client.SendAsync(request);\nConsole.WriteLine((int)response.StatusCode);\n"
        + "Console.WriteLine(await response.Content.ReadAsStringAsync());\n";

    private const string DigestNoteText = "Digest authentication must be computed by the caller (RFC 7616).";

    private static readonly CSharpHttpClientCodeGenerator Gen = new();

    private static string Head(string method, string url) =>
        $"using var client = new HttpClient();\nusing var request = new HttpRequestMessage({method}, \"{url}\");\n";

    [Fact]
    public void Identity()
    {
        Assert.Equal("csharp-httpclient", Gen.Id);
        Assert.Equal("C# (HttpClient)", Gen.DisplayName);
        Assert.Equal("csharp", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        Head("HttpMethod.Get", "https://api.example.com/users?id=1&name=a%20b")
        + "request.Headers.TryAddWithoutValidation(\"Accept\", \"application/json\");\n" + Tail,
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        "using System.Text;\n\n" + Head("HttpMethod.Post", "https://api.example.com/users")
        + "request.Headers.TryAddWithoutValidation(\"Accept\", \"application/json\");\n"
        + "request.Content = new StringContent(\"{\\\"text\\\":\\\"say \\\\\\\"hi\\\\\\\" C:\\\\\\\\dir\\\\nline2 é\\\"}\", Encoding.UTF8, \"application/json\");\n"
        + Tail,
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void PostForm() => Assert.Equal(
        Head("HttpMethod.Post", "https://api.example.com/form")
        + "request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { [\"a\"] = \"1\", [\"b\"] = \"x y\" });\n" + Tail,
        Gen.Generate(CodeSamples.PostForm));

    [Fact]
    public void PostMultipart() => Assert.Equal(
        Head("HttpMethod.Post", "https://api.example.com/upload")
        + "request.Content = new MultipartFormDataContent\n{\n    { new StringContent(\"Report\"), \"title\" },\n"
        + "    { new StreamContent(File.OpenRead(\"/tmp/report.pdf\")), \"file\", \"report.pdf\" },\n};\n" + Tail,
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        Head("HttpMethod.Put", "https://api.example.com/blob")
        + "request.Content = new StreamContent(File.OpenRead(\"/tmp/data.bin\"));\n"
        + "request.Content.Headers.TryAddWithoutValidation(\"Content-Type\", \"application/octet-stream\");\n" + Tail,
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Contains(
        "request.Headers.TryAddWithoutValidation(\"X-Quote\", \"say \\\"hi\\\" it's\");\n", Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal(
        Head("HttpMethod.Get", "https://{{host}}/users") + Tail, Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.StartsWith(
        "// " + DigestNoteText + "\nusing var client", Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Using_system_text_is_only_emitted_for_text_bodies()
    {
        Assert.DoesNotContain("using System.Text;", Gen.Generate(CodeSamples.GetSimple));
        Assert.DoesNotContain("using System.Text;", Gen.Generate(CodeSamples.PostForm));
        Assert.StartsWith("using System.Text;\n", Gen.Generate(CodeSamples.PostJson));
        Assert.StartsWith(
            "// " + DigestNoteText + "\nusing System.Text;\n",
            Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Text("a", null), DigestNoteText)));
    }

    [Fact]
    public void Text_body_without_content_type_and_charset_dropped()
    {
        Assert.Contains("new StringContent(\"a\", Encoding.UTF8);", Gen.Generate(
            new CodeRequest("POST", "https://x.test/", [], new CodeBody.Text("a", null), null)));
        Assert.Contains("new StringContent(\"a\", Encoding.UTF8, \"text/plain\");", Gen.Generate(
            new CodeRequest("POST", "https://x.test/", [new("content-type", "text/plain; charset=latin1")], new CodeBody.Text("a", null), null)));
    }

    [Fact]
    public void Content_headers_go_on_the_content_after_it_is_created()
    {
        var text = Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [new("Content-Language", "fr"), new("X-A", "1")], new CodeBody.Text("a", null), null));
        Assert.Contains("request.Headers.TryAddWithoutValidation(\"X-A\", \"1\");\n", text);
        Assert.DoesNotContain("request.Headers.TryAddWithoutValidation(\"Content-Language\"", text);
        Assert.True(text.IndexOf("request.Content = ", StringComparison.Ordinal) < text.IndexOf("request.Content.Headers.TryAdd", StringComparison.Ordinal));
    }

    [Fact]
    public void Content_headers_without_a_body_stay_on_the_request() => Assert.Contains(
        "request.Headers.TryAddWithoutValidation(\"Content-Length\", \"0\");",
        Gen.Generate(new CodeRequest("GET", "https://x.test/", [new("Content-Length", "0")], new CodeBody.None(), null)));

    [Fact]
    public void Repeated_form_field_becomes_a_list() => Assert.Contains(
        "new FormUrlEncodedContent(new List<KeyValuePair<string, string>> { new(\"a\", \"1\"), new(\"b\", \"3\"), new(\"a\", \"2\") })",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [], new CodeBody.Form([new("a", "1"), new("b", "3"), new("a", "2")]), null)));

    [Fact]
    public void Form_with_other_content_type_replaces_it() => Assert.Contains(
        "request.Content.Headers.Remove(\"Content-Type\");\nrequest.Content.Headers.TryAddWithoutValidation(\"Content-Type\", \"text/plain\");\n",
        Gen.Generate(new CodeRequest("POST", "https://x.test/", [new("Content-Type", "text/plain")], new CodeBody.Form([new("a", "1")]), null)));

    [Theory]
    [InlineData("GET", "HttpMethod.Get")]
    [InlineData("POST", "HttpMethod.Post")]
    [InlineData("PUT", "HttpMethod.Put")]
    [InlineData("DELETE", "HttpMethod.Delete")]
    [InlineData("HEAD", "HttpMethod.Head")]
    [InlineData("OPTIONS", "HttpMethod.Options")]
    [InlineData("PATCH", "HttpMethod.Patch")]
    [InlineData("PURGE", "new HttpMethod(\"PURGE\")")]
    public void Methods(string method, string expected) => Assert.Contains(
        $"new HttpRequestMessage({expected}, ", Gen.Generate(new CodeRequest(method, "https://x.test/", [], new CodeBody.None(), null)));

    [Theory]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("a\nb", "\"a\\nb\"")]
    [InlineData("a\rb", "\"a\\rb\"")]
    [InlineData("a\tb", "\"a\\tb\"")]
    [InlineData("a\0b", "\"a\\0b\"")]
    [InlineData("a\u0001b", "\"a\\u0001b\"")]
    [InlineData("é€", "\"é€\"")]
    public void Escape_handles_special_characters(string input, string expected) =>
        Assert.Equal(expected, CSharpHttpClientCodeGenerator.Escape(input));
}
