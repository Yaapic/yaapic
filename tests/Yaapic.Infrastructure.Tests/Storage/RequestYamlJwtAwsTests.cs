using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class RequestYamlJwtAwsTests
{
    private const string Header = "version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\n";

    private const string Pem = "-----BEGIN PRIVATE KEY-----\nMIGHAgEA\nabc=\n-----END PRIVATE KEY-----\n";

    private static HttpRequestDefinition With(RequestAuth auth) => HttpRequestDefinition.Create("r") with { Auth = auth };

    private static HttpRequestDefinition RoundTrip(HttpRequestDefinition r) =>
        RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(r));

    private static RequestAuth ReadAuth(string authYaml) => RequestYamlSerializer.Deserialize(Header + authYaml).Auth;

    private static JwtSettings Full(JwtAlgorithm algorithm) => new(
        algorithm, "{{jwtSecret}}", true, Pem, "{\"sub\":\"{{user}}\",\"q\":\"a: 'b'\"}", "{\"kid\":\"1\"}", false, 300,
        JwtPlacement.Query, "Token", "tok");

    public static TheoryData<JwtAlgorithm> Algorithms() => new(Enum.GetValues<JwtAlgorithm>());

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void JwtRoundTripKeepsEveryField(JwtAlgorithm algorithm)
    {
        var back = RoundTrip(With(RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = Full(algorithm) }));
        Assert.Equal(AuthKind.Jwt, back.Auth.Kind);
        Assert.Equal(Full(algorithm), back.Auth.Jwt);
    }

    [Fact]
    public void PemWithNewlinesAndQuotedPayloadSurvive()
    {
        var jwt = Full(JwtAlgorithm.Rs256);
        var back = RoundTrip(With(RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = jwt })).Auth.Jwt!;
        Assert.Equal(Pem, back.PrivateKey);
        Assert.Equal(jwt.Payload, back.Payload);
    }

    [Fact]
    public void JwtDefaultsAndUnknownAlgorithm()
    {
        Assert.Equal(JwtSettings.Default, ReadAuth("auth:\n  type: jwt\n").Jwt);
        var auth = ReadAuth("auth:\n  type: jwt\n  jwt:\n    algorithm: none\n    placement: z\n    secret: s\n");
        Assert.Equal(JwtSettings.Default with { Secret = "s" }, auth.Jwt);
    }

    [Fact]
    public void AwsRoundTrip()
    {
        var aws = new AwsV4Settings("{{ak}}", "{{sk}}", "tok", "eu-west-1", "s3");
        var back = RoundTrip(With(RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = aws }));
        Assert.Equal(AuthKind.AwsV4, back.Auth.Kind);
        Assert.Equal(aws, back.Auth.AwsV4);
        Assert.Equal(AwsV4Settings.Default, ReadAuth("auth:\n  type: awsv4\n").AwsV4);
    }

    [Fact]
    public void DigestKeepsUsernameAndPassword()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Digest, Username = "u", Password = "{{pw}}" };
        var back = RoundTrip(With(auth)).Auth;
        Assert.Equal(AuthKind.Digest, back.Kind);
        Assert.Equal("u", back.Username);
        Assert.Equal("{{pw}}", back.Password);
    }

    [Fact]
    public void SectionsOnOtherKindsAreIgnoredAndNotWritten()
    {
        var auth = ReadAuth("auth:\n  type: bearer\n  token: t\n  jwt:\n    secret: s\n  awsv4:\n    region: r\n");
        Assert.Null(auth.Jwt);
        Assert.Null(auth.AwsV4);
        var yaml = RequestYamlSerializer.Serialize(HttpRequestDefinition.Create("r"));
        Assert.DoesNotContain("jwt", yaml);
        Assert.DoesNotContain("awsv4", yaml);
    }

    [Fact]
    public void OutputIsStable()
    {
        var yaml = RequestYamlSerializer.Serialize(With(RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = Full(JwtAlgorithm.Es384) }));
        Assert.Equal(yaml, RequestYamlSerializer.Serialize(RequestYamlSerializer.Deserialize(yaml)));
        Assert.Contains("algorithm: ES384", yaml);
    }
}
