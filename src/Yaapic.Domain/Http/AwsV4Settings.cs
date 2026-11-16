namespace Yaapic.Domain.Http;

/// <summary>The AWS Signature Version 4 configuration of a request.</summary>
/// <param name="AccessKeyId">The access key identifier.</param>
/// <param name="SecretAccessKey">The secret access key.</param>
/// <param name="SessionToken">The optional session token.</param>
/// <param name="Region">The AWS region.</param>
/// <param name="Service">The AWS service name.</param>
public sealed record AwsV4Settings(
    string AccessKeyId,
    string SecretAccessKey,
    string SessionToken,
    string Region,
    string Service)
{
    /// <summary>The defaults of a new configuration: every string is empty.</summary>
    public static AwsV4Settings Default { get; } = new("", "", "", "", "");
}
