using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class ClientCertificateTests
{
    private static ClientCertificate Make(string name = "n", string host = "api.example.com", string cert = "c.pfx") =>
        new(Guid.Empty, name, host, ClientCertificateFormat.Pfx, cert, "", false);

    [Theory]
    [InlineData("api.example.com")]
    [InlineData("*.example.com")]
    [InlineData("api.example.com:8443")]
    [InlineData("*.example.com:8443")]
    [InlineData("localhost")]
    [InlineData("10.0.0.5")]
    public void Valid_entries_have_no_issue(string host) =>
        Assert.Empty(ClientCertificateValidator.Validate(Make(host: host)));

    [Theory]
    [InlineData("")]
    [InlineData("api example.com")]
    [InlineData("https://api.example.com")]
    [InlineData("api.example.com/path")]
    [InlineData("*")]
    [InlineData("*example.com")]
    [InlineData("api.*.com")]
    [InlineData("**.example.com")]
    [InlineData("*.*.example.com")]
    [InlineData("api.example.com:0")]
    [InlineData("api.example.com:65536")]
    [InlineData("api.example.com:abc")]
    [InlineData("api.example.com:")]
    [InlineData("api.example.com:-1")]
    public void Invalid_host_patterns_are_reported(string host) =>
        Assert.Equal([ClientCertificateIssue.HostPatternInvalid], ClientCertificateValidator.Validate(Make(host: host)));

    [Fact]
    public void Empty_name_is_reported() =>
        Assert.Equal([ClientCertificateIssue.NameEmpty], ClientCertificateValidator.Validate(Make(name: " ")));

    [Fact]
    public void Empty_certificate_path_is_reported() =>
        Assert.Equal([ClientCertificateIssue.CertificatePathEmpty], ClientCertificateValidator.Validate(Make(cert: "")));

    [Fact]
    public void Key_path_is_never_required() =>
        Assert.Empty(ClientCertificateValidator.Validate(Make() with { Format = ClientCertificateFormat.Pem, KeyPath = "" }));

    [Fact]
    public void Records_with_the_same_values_are_equal() => Assert.Equal(Make(), Make());
}
