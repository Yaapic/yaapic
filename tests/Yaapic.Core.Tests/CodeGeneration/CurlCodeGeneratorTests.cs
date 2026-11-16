using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class CurlCodeGeneratorTests
{
    private static readonly CurlCodeGenerator Gen = new();

    [Fact]
    public void Identity()
    {
        Assert.Equal("curl", Gen.Id);
        Assert.Equal("cURL", Gen.DisplayName);
        Assert.Equal("shell", Gen.EditorLanguage);
    }

    [Fact]
    public void GetSimple() => Assert.Equal(
        "curl 'https://api.example.com/users?id=1&name=a%20b' \\\n  --header 'Accept: application/json'\n",
        Gen.Generate(CodeSamples.GetSimple));

    [Fact]
    public void PostJson() => Assert.Equal(
        "curl --request POST 'https://api.example.com/users' \\\n"
        + "  --header 'Accept: application/json' \\\n"
        + "  --header 'Content-Type: application/json' \\\n"
        + "  --data-raw '{\"text\":\"say \\\"hi\\\" C:\\\\dir\\nline2 é\"}'\n",
        Gen.Generate(CodeSamples.PostJson));

    [Fact]
    public void PostForm() => Assert.Equal(
        "curl --request POST 'https://api.example.com/form' \\\n"
        + "  --header 'Content-Type: application/x-www-form-urlencoded' \\\n"
        + "  --data-urlencode 'a=1' \\\n"
        + "  --data-urlencode 'b=x y'\n",
        Gen.Generate(CodeSamples.PostForm));

    [Fact]
    public void PostMultipart() => Assert.Equal(
        "curl --request POST 'https://api.example.com/upload' \\\n"
        + "  --form 'title=Report' \\\n"
        + "  --form 'file=@/tmp/report.pdf'\n",
        Gen.Generate(CodeSamples.PostMultipart));

    [Fact]
    public void PutFile() => Assert.Equal(
        "curl --request PUT 'https://api.example.com/blob' \\\n"
        + "  --header 'Content-Type: application/octet-stream' \\\n"
        + "  --data-binary '@/tmp/data.bin'\n",
        Gen.Generate(CodeSamples.PutFile));

    [Fact]
    public void HeadersAwkward() => Assert.Equal(
        "curl 'https://api.example.com/h' \\\n  --header 'X-Quote: say \"hi\" it'\\''s'\n",
        Gen.Generate(CodeSamples.HeadersAwkward));

    [Fact]
    public void Unresolved() => Assert.Equal("curl 'https://{{host}}/users'\n", Gen.Generate(CodeSamples.Unresolved));

    [Fact]
    public void DigestNote() => Assert.Equal(
        "# Digest authentication must be computed by the caller (RFC 7616).\ncurl 'https://api.example.com/digest'\n",
        Gen.Generate(CodeSamples.DigestNote));

    [Fact]
    public void Head() => Assert.Equal(
        "curl --head 'https://x.test/'\n",
        Gen.Generate(new CodeRequest("HEAD", "https://x.test/", [], new CodeBody.None(), null)));

    [Fact]
    public void Multipart_text_starting_with_at_or_lt_uses_form_string() => Assert.Equal(
        "curl --request POST 'https://x.test/' \\\n  --form-string 'a=@x' \\\n  --form-string 'b=<y'\n",
        Gen.Generate(new CodeRequest(
            "POST", "https://x.test/", [], new CodeBody.Multipart([new("a", "@x", null), new("b", "<y", null)]), null)));

    [Fact]
    public async Task Generated_command_sends_the_expected_request()
    {
        var sh = FindOnPath("sh");
        if (sh is null || FindOnPath("curl") is null)
        {
            Assert.Skip("curl or sh is not on the PATH.");
        }

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
            var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (await reader.ReadLineAsync(TestContext.Current.CancellationToken) is { Length: > 0 } line)
            {
                lines.Add(line);
            }

            await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
            return lines;
        });

        var script = Gen.Generate(CodeSamples.GetSimple with { Url = $"http://127.0.0.1:{port}/users?id=1" });
        var psi = new ProcessStartInfo(sh, ["-c", "--", script]) { UseShellExecute = false, RedirectStandardOutput = true };
        using var process = Process.Start(psi)!;
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        var received = await serve.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal("GET /users?id=1 HTTP/1.1", received[0]);
        Assert.Contains("Accept: application/json", received);
    }

    private static string? FindOnPath(string name)
    {
        string[] names = OperatingSystem.IsWindows() ? [name + ".exe"] : [name];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            foreach (var n in names)
            {
                var p = Path.Combine(dir, n);
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        return null;
    }
}
