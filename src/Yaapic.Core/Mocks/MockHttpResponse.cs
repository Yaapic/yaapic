using Yaapic.Domain.Http;

namespace Yaapic.Core.Mocks;

/// <summary>A response produced by a mock server.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Reason">The reason phrase.</param>
/// <param name="Headers">The response headers.</param>
/// <param name="Body">The response body.</param>
/// <param name="Delay">The delay to wait before sending.</param>
/// <param name="OmitBody">Whether the body is withheld (answer to <c>HEAD</c>) while its length is still announced.</param>
public sealed record MockHttpResponse(
    int Status, string Reason, IReadOnlyList<KeyValueEntry> Headers, byte[] Body, TimeSpan Delay, bool OmitBody = false);
