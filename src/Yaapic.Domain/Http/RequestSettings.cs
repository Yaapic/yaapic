namespace Yaapic.Domain.Http;

/// <summary>Per-request sending settings.</summary>
public sealed record RequestSettings
{
    /// <summary>Creates the settings.</summary>
    /// <param name="followRedirects">Whether redirects are followed.</param>
    /// <param name="timeoutSeconds">The timeout in seconds; 0 means the application default.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is negative.</exception>
    public RequestSettings(bool followRedirects, int timeoutSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutSeconds);
        FollowRedirects = followRedirects;
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>Redirects are followed and the application default timeout applies.</summary>
    public static RequestSettings Default { get; } = new(true, 0);

    /// <summary>Gets a value indicating whether redirects are followed.</summary>
    public bool FollowRedirects { get; }

    /// <summary>Gets the timeout in seconds; 0 means the application default.</summary>
    public int TimeoutSeconds { get; }
}
