using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.Aws;

/// <summary>The result of an AWS Signature v4 computation.</summary>
/// <param name="Authorization">The <c>Authorization</c> header value.</param>
/// <param name="AmzDate">The <c>x-amz-date</c> value (<c>yyyyMMddTHHmmssZ</c>).</param>
/// <param name="ExtraHeaders">The headers the caller must add: <c>x-amz-date</c>, and <c>x-amz-security-token</c> when set.</param>
public sealed record AwsSignature(string Authorization, string AmzDate, IReadOnlyList<KeyValueEntry> ExtraHeaders);
