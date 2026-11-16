using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.Aws;

/// <summary>Everything needed to compute an AWS Signature v4 for one request.</summary>
/// <param name="Method">The HTTP method, e.g. <c>GET</c>.</param>
/// <param name="Url">The absolute request URL.</param>
/// <param name="Headers">The request headers; only <c>content-type</c> and <c>x-amz-content-sha256</c> are signed.</param>
/// <param name="PayloadHash">The lowercase hex SHA-256 of the body, or <see cref="AwsSigV4.UnsignedPayload"/>.</param>
/// <param name="AccessKeyId">The access key id.</param>
/// <param name="SecretAccessKey">The secret access key.</param>
/// <param name="SessionToken">The optional session token (temporary credentials).</param>
/// <param name="Region">The AWS region, e.g. <c>us-east-1</c>.</param>
/// <param name="Service">The AWS service, e.g. <c>s3</c>.</param>
/// <param name="Time">The signing time (converted to UTC).</param>
public sealed record AwsSigningInput(
    string Method,
    Uri Url,
    IReadOnlyList<KeyValueEntry> Headers,
    string PayloadHash,
    string AccessKeyId,
    string SecretAccessKey,
    string? SessionToken,
    string Region,
    string Service,
    DateTimeOffset Time);
