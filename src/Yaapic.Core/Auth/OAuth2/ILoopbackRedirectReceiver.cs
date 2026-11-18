namespace Yaapic.Core.Auth.OAuth2;

/// <summary>Waits for the browser to be redirected to a loopback address after the authorization request.</summary>
public interface ILoopbackRedirectReceiver
{
    /// <summary>Listens until the first request to <paramref name="path"/>, answers it, then stops listening.</summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="path">The path to expect.</param>
    /// <param name="successHtml">The page to answer the browser with.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The request target, for example <c>/callback?code=abc&amp;state=xyz</c>.</returns>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    /// <exception cref="InvalidOperationException">The port is busy; the message is user-presentable.</exception>
    Task<string> WaitForRequestTargetAsync(int port, string path, string successHtml, CancellationToken ct);
}
