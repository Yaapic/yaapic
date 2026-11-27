using Yaapic.Core.Mocks;

namespace Yaapic.Infrastructure.Mocks;

/// <summary>The outcome of reading one request from a connection.</summary>
internal abstract record Http11ReadResult
{
    /// <summary>A request was read.</summary>
    /// <param name="Value">The request.</param>
    /// <param name="KeepAlive">Whether the connection stays open after the response.</param>
    internal sealed record Request(MockHttpRequest Value, bool KeepAlive) : Http11ReadResult;

    /// <summary>The stream ended (or timed out) cleanly before a request.</summary>
    internal sealed record Closed : Http11ReadResult;

    /// <summary>The request is refused.</summary>
    /// <param name="Status">The status code to answer with.</param>
    internal sealed record Error(int Status) : Http11ReadResult;
}
