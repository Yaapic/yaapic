using Yaapic.Core.Auth.OAuth2;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IExternalBrowser"/> that records the opened URLs.</summary>
public sealed class FakeExternalBrowser : IExternalBrowser
{
    /// <summary>Gets or sets the shared event log used to check ordering with <see cref="FakeLoopbackRedirectReceiver"/>.</summary>
    public List<string> Log { get; set; } = [];

    /// <summary>Gets the URLs opened so far.</summary>
    public List<Uri> Opened { get; } = [];

    /// <summary>Gets a task that completes once a URL has been opened.</summary>
    public Task WhenOpened => _opened.Task;

    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public Task OpenAsync(Uri url, CancellationToken ct)
    {
        Log.Add("open");
        Opened.Add(url);
        _opened.TrySetResult();
        return Task.CompletedTask;
    }
}
