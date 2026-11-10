using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>A loopback HTTP server echoing requests as JSON, with <c>/status/{code}</c> and <c>/delay/{ms}</c> routes, <c>/redirect</c> answering 302 to <c>/echo</c>, <c>/json</c> (a small document), <c>/headers</c> (the request headers), <c>/set-cookie</c> (two cookies) and <c>/count</c> (a per-call counter).</summary>
public sealed class LoopbackServer : IDisposable
{
    private static int _counter;

    private readonly HttpListener _listener = new();
    private readonly ConcurrentQueue<string> _hits = new();

    public LoopbackServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    public string BaseUrl { get; }

    /// <summary>Gets the raw URLs received so far, in arrival order.</summary>
    public IReadOnlyCollection<string> Hits => _hits;

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
            _hits.Enqueue(context.Request.RawUrl!);
            if (await TryAnswerFixedAsync(context, path))
            {
                return;
            }

            if (path.StartsWith("/status/", StringComparison.Ordinal))
            {
                context.Response.StatusCode = int.Parse(path["/status/".Length..]);
            }
            else if (path.StartsWith("/delay/", StringComparison.Ordinal))
            {
                await Task.Delay(int.Parse(path["/delay/".Length..]));
            }

            else if (path == "/redirect")
            {
                context.Response.StatusCode = 302;
                context.Response.RedirectLocation = "/echo";
            }

            using var raw = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(raw);
            var bodyBytes = raw.ToArray();
            var stream = new MemoryStream();
            using (var json = new Utf8JsonWriter(stream))
            {
                json.WriteStartObject();
                json.WriteString("method", context.Request.HttpMethod);
                json.WriteString("path", context.Request.RawUrl);
                json.WriteStartObject("headers");
                foreach (string name in context.Request.Headers)
                {
                    json.WriteString(name, context.Request.Headers[name]);
                }

                json.WriteEndObject();
                json.WriteString("body", Encoding.UTF8.GetString(bodyBytes));
                json.WriteString("bodyBase64", Convert.ToBase64String(bodyBytes));
                json.WriteEndObject();
            }

            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = stream.Length;
            await context.Response.OutputStream.WriteAsync(stream.ToArray());
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // The client went away (cancellation scenario).
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                // Same: nothing left to close.
            }
        }
    }

    private static async Task<bool> TryAnswerFixedAsync(HttpListenerContext context, string path)
    {
        string json;
        switch (path)
        {
            case "/json":
                json = "{\"token\":\"abc123\",\"items\":[1,2,3]}";
                break;
            case "/headers":
                using (var stream = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        foreach (var name in context.Request.Headers.AllKeys)
                        {
                            writer.WriteString(name!, context.Request.Headers[name]);
                        }

                        writer.WriteEndObject();
                    }

                    json = Encoding.UTF8.GetString(stream.ToArray());
                }

                break;
            case "/set-cookie":
                context.Response.AppendHeader("Set-Cookie", "a=1; Path=/");
                context.Response.AppendHeader("Set-Cookie", "b=2; Path=/");
                json = "{}";
                break;
            case "/count":
                json = "{\"count\":" + Interlocked.Increment(ref _counter) + "}";
                break;
            default:
                return false;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        return true;
    }
}
