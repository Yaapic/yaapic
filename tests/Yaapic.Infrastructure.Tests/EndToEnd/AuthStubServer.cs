using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Yaapic.Core.Auth.Aws;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>A loopback identity and API server: <c>/token</c>, <c>/authorize</c>, <c>/api</c>, <c>/digest</c>, <c>/jwt</c> and <c>/aws</c>.</summary>
public sealed partial class AuthStubServer : IDisposable
{
    public const string ClientId = "client-1";
    public const string ClientSecret = "s3cret-value";
    public const string Username = "alice";
    public const string Password = "wonderland";
    public const string JwtSecret = "jwt-shared-secret";
    public const string AwsAccessKey = "AKIDEXAMPLE";
    public const string AwsSecret = "aws-secret-key";
    public const string AwsRegion = "us-east-1";
    public const string AwsService = "svc";
    public const string DigestRealm = "yaapic";
    public const string DigestNonce = "fixednonce123";

    private readonly HttpListener _listener = new();
    private readonly ConcurrentQueue<string> _grants = new();
    private readonly DateTimeOffset _awsTime;

    public AuthStubServer(DateTimeOffset awsTime)
    {
        _awsTime = awsTime;
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)probe.LocalEndpoint).Port}";
        probe.Stop();
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public string BaseUrl { get; }

    /// <summary>Gets or sets the <c>expires_in</c> the token endpoint answers.</summary>
    public int ExpiresIn { get; set; } = 3600;

    /// <summary>Gets or sets a value indicating whether the token endpoint issues a refresh token.</summary>
    public bool IssueRefreshToken { get; set; } = true;

    /// <summary>Gets the <c>grant_type</c> of every token request, in arrival order.</summary>
    public IReadOnlyCollection<string> Grants => _grants;

    /// <summary>Gets a value indicating whether the authorization-code exchange carried the verifier of the challenge seen at <c>/authorize</c>.</summary>
    public bool PkceVerified { get; private set; }

    public void Dispose() => _listener.Close();

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url!.AbsolutePath;
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var (status, json) = path switch
            {
                "/token" => Token(context, body),
                "/authorize" => Authorize(context),
                "/api" => Api(context),
                "/digest" => Digest(context),
                "/jwt" => Jwt(context),
                "/aws" => Aws(context),
                _ => (404, "{}"),
            };
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // The client went away.
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                // Nothing left to close.
            }
        }
    }

    private (int Status, string Json) Token(HttpListenerContext context, string body)
    {
        var form = Query(body);
        var grant = form.GetValueOrDefault("grant_type", "");
        _grants.Enqueue(grant);
        var id = form.GetValueOrDefault("client_id", "");
        var secret = form.GetValueOrDefault("client_secret", "");
        if (context.Request.Headers["Authorization"] is { } basic && basic.StartsWith("Basic ", StringComparison.Ordinal))
        {
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(basic["Basic ".Length..])).Split(':', 2);
            id = Uri.UnescapeDataString(pair[0]);
            secret = Uri.UnescapeDataString(pair[1]);
        }

        if (id != ClientId || secret != ClientSecret)
        {
            return (401, """{"error":"invalid_client","error_description":"Client authentication failed"}""");
        }

        var valid = grant switch
        {
            "client_credentials" => true,
            "password" => form.GetValueOrDefault("username") == Username && form.GetValueOrDefault("password") == Password,
            "refresh_token" => form.GetValueOrDefault("refresh_token") == "r1",
            "authorization_code" => ExchangeCode(form),
            _ => false,
        };
        if (!valid)
        {
            return (400, """{"error":"invalid_grant","error_description":"Grant rejected"}""");
        }

        var refresh = IssueRefreshToken ? ",\"refresh_token\":\"r1\"" : "";
        return (200, $"{{\"access_token\":\"t1\",\"token_type\":\"Bearer\",\"expires_in\":{ExpiresIn}{refresh}}}");
    }

    private string _challengeSeen = "";

    private bool ExchangeCode(Dictionary<string, string> form)
    {
        if (form.GetValueOrDefault("code") != "code-1" || !form.TryGetValue("code_verifier", out var verifier))
        {
            return false;
        }

        PkceVerified = Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) == _challengeSeen;
        return PkceVerified;
    }

    private (int Status, string Json) Authorize(HttpListenerContext context)
    {
        var query = Query(context.Request.Url!.Query.TrimStart('?'));
        _challengeSeen = query.GetValueOrDefault("code_challenge", "");
        context.Response.RedirectLocation = $"{query["redirect_uri"]}?code=code-1&state={Uri.EscapeDataString(query["state"])}";
        return (302, "{}");
    }

    private static (int Status, string Json) Api(HttpListenerContext context) =>
        context.Request.Headers["Authorization"] == "Bearer t1" ? (200, """{"ok":true}""") : (401, """{"ok":false}""");

    private (int Status, string Json) Digest(HttpListenerContext context)
    {
        var challenge = $"Digest realm=\"{DigestRealm}\", nonce=\"{DigestNonce}\", qop=\"auth\", algorithm=MD5, opaque=\"op\"";
        if (context.Request.Headers["Authorization"] is not { } header || !header.StartsWith("Digest ", StringComparison.Ordinal))
        {
            context.Response.AddHeader("WWW-Authenticate", challenge);
            return (401, "{}");
        }

        var fields = DigestField().Matches(header).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value);
        static string Md5(string s) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(s)));
        var ha1 = Md5($"{Username}:{DigestRealm}:{Password}");
        var ha2 = Md5($"GET:{fields["uri"]}");
        var expected = Md5($"{ha1}:{DigestNonce}:{fields["nc"]}:{fields["cnonce"]}:auth:{ha2}");
        if (fields["username"] == Username && fields["nonce"] == DigestNonce && fields["response"] == expected)
        {
            return (200, """{"digest":"ok"}""");
        }

        context.Response.AddHeader("WWW-Authenticate", challenge);
        return (401, "{}");
    }

    private static (int Status, string Json) Jwt(HttpListenerContext context)
    {
        var token = context.Request.Headers["Authorization"]?.Replace("Bearer ", "", StringComparison.Ordinal).Split('.');
        if (token is not { Length: 3 })
        {
            return (401, "{}");
        }

        var signature = Base64Url.Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(JwtSecret), Encoding.ASCII.GetBytes($"{token[0]}.{token[1]}")));
        return signature == token[2] ? (200, Encoding.UTF8.GetString(Base64Url.Decode(token[1]))) : (401, "{}");
    }

    private (int Status, string Json) Aws(HttpListenerContext context)
    {
        var request = context.Request;
        var hash = request.Headers["x-amz-content-sha256"] ?? "";
        var expected = AwsSigV4.Sign(new AwsSigningInput(
            request.HttpMethod,
            new Uri(BaseUrl + request.RawUrl),
            [new KeyValueEntry("x-amz-content-sha256", hash)],
            hash,
            AwsAccessKey,
            AwsSecret,
            null,
            AwsRegion,
            AwsService,
            _awsTime));
        return request.Headers["Authorization"] == expected.Authorization ? (200, """{"aws":"ok"}""") : (403, """{"aws":"denied"}""");
    }

    private static Dictionary<string, string> Query(string text) =>
        text.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : "");

    [GeneratedRegex("""(\w+)=(?:"((?:[^"\\]|\\.)*)"|([^,\s]+))""")]
    private static partial Regex DigestField();
}
