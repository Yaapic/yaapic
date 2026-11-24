using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Infrastructure.Http;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;

// Spike M6-02: concurrent scripted sends under load. Usage: LoadSpike [seconds] [N,N,...]
var seconds = args.Length > 0 ? int.Parse(args[0]) : 20;
int[] counts = args.Length > 1 ? [.. args[1].Split(',').Select(int.Parse)] : [10, 50, 100, 200];

using var server = new KeepAliveServer();
using var transport = new HttpClientTransport(DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(30));
var engines = new ScriptEngineRegistry();
engines.Add(new QuickJsEngine());
engines.Add(new LuaEngine());
var sender = new ScriptedRequestSender(new RequestSender(transport), engines, TimeProvider.System);

const string jsPre = "yaapic.variables.set('t', Date.now());";
const string jsPost = "yaapic.test('ok', () => yaapic.expect(yaapic.response.code).to.equal(200));";
const string luaPre = "yaapic.variables.set('t', 'x')";
const string luaPost = "yaapic.test('ok', function() yaapic.expect(yaapic.response.code).to.equal(200) end)";

Console.WriteLine($"Machine: {Environment.ProcessorCount} logical cores, {RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}, {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0:F1} GiB RAM, AOT={!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"Thread start cost (2 MiB stack), median: {ThreadStartMicros():F0} us");
Console.WriteLine("kind;N;req/s;p50 ms;p95 ms;p99 ms;errors;scriptFailures;timedOut;peakWS MiB;peakThreads");
foreach (var (kind, pre, post, language) in new[] { ("none", "", "", ScriptLanguage.JavaScript), ("js", jsPre, jsPost, ScriptLanguage.JavaScript), ("lua", luaPre, luaPost, ScriptLanguage.Lua) })
{
    foreach (var n in counts)
    {
        await RunAsync(kind, pre, post, language, n);
    }
}

async Task RunAsync(string kind, string pre, string post, ScriptLanguage language, int n)
{
    var source = pre;
    var scripts = new ScriptSet(new ScriptBlock(language, pre), new ScriptBlock(language, post));
    var request = HttpRequestDefinition.Create("load") with { Url = server.BaseUrl + "/", Scripts = scripts };
    var chain = ScriptChain.Empty.WithRequest("load", scripts);
    var input = new ScriptedSendInput(request, VariableScopes.None, chain, false, false);

    var latencies = new List<double>[n];
    long errors = 0, scriptFailures = 0, timedOut = 0, count = 0;
    long peakWs = 0, diagnosed = 0;
    var peakThreads = 0;
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    using var sampler = new CancellationTokenSource();
    var sampling = Task.Run(async () =>
    {
        var p = Process.GetCurrentProcess();
        while (!sampler.IsCancellationRequested)
        {
            p.Refresh();
            peakWs = Math.Max(peakWs, p.WorkingSet64);
            peakThreads = Math.Max(peakThreads, p.Threads.Count);
            try { await Task.Delay(250, sampler.Token); } catch (OperationCanceledException) { }
        }
    });

    var clock = Stopwatch.StartNew();
    var loops = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
    {
        var mine = latencies[i] = [];
        while (!cts.IsCancellationRequested)
        {
            var start = Stopwatch.GetTimestamp();
            var report = await sender.SendAsync(input, CancellationToken.None);
            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (cts.IsCancellationRequested) { break; }
            if (Interlocked.Increment(ref diagnosed) == 1 && source.Length > 0)
            {
                Console.WriteLine($"  first send: send={report.Report?.Result.IsSuccess} runs=[{string.Join(" | ", report.Runs.Select(r => $"{r.Status} {r.Error?.Message}"))}] tests=[{string.Join(" | ", report.Tests.Select(t => $"{t}"))}]");
            }

            Interlocked.Increment(ref count);
            if (report.Runs.Any(r => r.Status == ScriptRunStatus.TimedOut)) { Interlocked.Increment(ref timedOut); }
            if (report.Runs.Any(r => !r.Succeeded)) { Interlocked.Increment(ref scriptFailures); }
            if (report.Report?.Result.IsSuccess != true || report.Tests.Any(t => !t.Passed)) { Interlocked.Increment(ref errors); }
            mine.Add(ms);
        }
    })).ToArray();
    await Task.WhenAll(loops);
    clock.Stop();
    await sampler.CancelAsync();
    await sampling;

    var all = latencies.Where(l => l is not null).SelectMany(l => l).Order().ToArray();
    double P(double q) => all.Length == 0 ? 0 : all[Math.Min(all.Length - 1, (int)(q * all.Length))];
    Console.WriteLine($"{kind};{n};{count / clock.Elapsed.TotalSeconds:F0};{P(.5):F1};{P(.95):F1};{P(.99):F1};{errors};{scriptFailures};{timedOut};{peakWs / 1048576.0:F0};{peakThreads}");
}

static double ThreadStartMicros()
{
    List<double> samples = [];
    for (var i = 0; i < 500; i++)
    {
        var start = Stopwatch.GetTimestamp();
        var tcs = new TaskCompletionSource();
        Thread t = new(() => tcs.SetResult(), 2 * 1024 * 1024) { IsBackground = true };
        t.Start();
        tcs.Task.Wait();
        samples.Add(Stopwatch.GetElapsedTime(start).TotalMicroseconds);
    }

    samples.Sort();
    return samples[samples.Count / 2];
}

/// <summary>A keep-alive HTTP/1.1 server answering 200 {"ok":true} on 127.0.0.1.</summary>
sealed class KeepAliveServer : IDisposable
{
    private static readonly byte[] Response = Encoding.ASCII.GetBytes(
        "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 11\r\nConnection: keep-alive\r\n\r\n{\"ok\":true}");

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();

    public KeepAliveServer()
    {
        _listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        _ = Task.Run(AcceptAsync);
    }

    public string BaseUrl { get; }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                client.NoDelay = true;
                _ = Task.Run(() => ServeAsync(client));
            }
        }
        catch (Exception) when (_stop.IsCancellationRequested)
        {
        }
    }

    private static async Task ServeAsync(TcpClient client)
    {
        using var owned = client;
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[8192];
            var filled = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(filled));
                if (read == 0) { return; }
                filled += read;

                // GET requests only: a request ends at the blank line.
                while (true)
                {
                    var end = buffer.AsSpan(0, filled).IndexOf("\r\n\r\n"u8);
                    if (end < 0) { break; }
                    await stream.WriteAsync(Response);
                    filled -= end + 4;
                    buffer.AsSpan(end + 4, filled).CopyTo(buffer);
                }
            }
        }
        catch (IOException)
        {
        }
    }
}
