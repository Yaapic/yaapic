using Yaapic.Core.Auth.OAuth2;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="ILoopbackRedirectReceiver"/> that returns a configured request target, or throws.</summary>
public sealed class FakeLoopbackRedirectReceiver : ILoopbackRedirectReceiver
{
    /// <summary>Gets or sets the shared event log used to check ordering with <see cref="FakeExternalBrowser"/>.</summary>
    public List<string> Log { get; set; } = [];

    /// <summary>Gets or sets the target to return; receives the <c>state</c> sent in the browser URL when a browser is linked.</summary>
    public Func<string> Target { get; set; } = () => "/callback?code=c1";

    /// <summary>Gets or sets an exception to throw instead of returning a target.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets or sets a task the answer waits for (never completes to simulate a silent user).</summary>
    public Task? Gate { get; set; }

    /// <summary>Gets the port received.</summary>
    public int Port { get; private set; }

    /// <summary>Gets the path received.</summary>
    public string? Path { get; private set; }

    /// <summary>Gets the success page received.</summary>
    public string? SuccessHtml { get; private set; }

    /// <inheritdoc/>
    public async Task<string> WaitForRequestTargetAsync(int port, string path, string successHtml, CancellationToken ct)
    {
        Log.Add("listen");
        Port = port;
        Path = path;
        SuccessHtml = successHtml;
        if (Failure is { } failure)
        {
            throw failure;
        }

        if (Gate is { } gate)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
        }

        await Task.Yield();
        return Target();
    }
}
