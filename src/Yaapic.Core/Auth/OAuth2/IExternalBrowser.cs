namespace Yaapic.Core.Auth.OAuth2;

/// <summary>Opens a URL in the system browser.</summary>
public interface IExternalBrowser
{
    /// <summary>Opens <paramref name="url"/>.</summary>
    /// <param name="url">The URL to open.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes once the browser was asked to open the URL.</returns>
    Task OpenAsync(Uri url, CancellationToken ct);
}
