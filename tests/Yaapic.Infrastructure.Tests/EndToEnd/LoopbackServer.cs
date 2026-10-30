using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>A loopback HTTP server echoing requests as JSON, with <c>/status/{code}</c> and <c>/delay/{ms}</c> routes.</summary>
public sealed class LoopbackServer : IDisposable
{
    private readonly HttpListener _listener = new();

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

    private static async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url!.AbsolutePath;
            if (path.StartsWith("/status/", StringComparison.Ordinal))
            {
                context.Response.StatusCode = int.Parse(path["/status/".Length..]);
            }
            else if (path.StartsWith("/delay/", StringComparison.Ordinal))
            {
                await Task.Delay(int.Parse(path["/delay/".Length..]));
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
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
                json.WriteString("body", body);
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
}
