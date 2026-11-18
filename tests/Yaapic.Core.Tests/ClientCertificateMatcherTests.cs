using Yaapic.Core.Certificates;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class ClientCertificateMatcherTests
{
    private static ClientCertificate E(string host, string name = "n") =>
        new(Guid.NewGuid(), name, host, ClientCertificateFormat.Pfx, "c.pfx", "", false);

    private static string? Pick(string url, params ClientCertificate[] entries) =>
        ClientCertificateMatcher.Find(entries, new Uri(url))?.HostPattern;

    [Fact]
    public void Each_rank_beats_the_next()
    {
        var r0 = E("a.example.com:443");
        var r1 = E("a.example.com");
        var r2 = E("*.example.com:443");
        var r3 = E("*.example.com");
        Assert.Same(r0, ClientCertificateMatcher.Find([r3, r2, r1, r0], new Uri("https://a.example.com/")));
        Assert.Same(r1, ClientCertificateMatcher.Find([r3, r2, r1], new Uri("https://a.example.com/")));
        Assert.Same(r2, ClientCertificateMatcher.Find([r3, r2], new Uri("https://a.example.com/")));
        Assert.Same(r3, ClientCertificateMatcher.Find([r3], new Uri("https://a.example.com/")));
    }

    [Fact]
    public void Ties_go_to_the_earliest_entry()
    {
        var first = E("a.example.com");
        var second = E("A.example.com");
        Assert.Same(first, ClientCertificateMatcher.Find([first, second], new Uri("https://a.example.com/")));
    }

    [Theory]
    [InlineData("https://a.example.com/", true)]
    [InlineData("https://a.b.example.com/", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("https://badexample.com/", false)]
    public void Wildcard_depth(string url, bool match) =>
        Assert.Equal(match, Pick(url, E("*.example.com")) is not null);

    [Theory]
    [InlineData("https://a.com/", "a.com:443", true)]
    [InlineData("https://a.com:8443/", "a.com:8443", true)]
    [InlineData("https://a.com:8443/", "a.com:443", false)]
    [InlineData("https://a.com:8443/", "a.com", true)]
    [InlineData("https://a.com/", "a.com:8443", false)]
    public void Port_rules(string url, string pattern, bool match) =>
        Assert.Equal(match, Pick(url, E(pattern)) is not null);

    [Fact]
    public void Http_never_matches() => Assert.Null(Pick("http://a.com/", E("a.com")));

    [Fact]
    public void Invalid_entries_are_skipped()
    {
        var bad = new ClientCertificate(Guid.NewGuid(), "", "a.com", ClientCertificateFormat.Pfx, "c.pfx", "", false);
        var good = E("*.com");
        Assert.Same(good, ClientCertificateMatcher.Find([bad, good], new Uri("https://a.com/")));
    }

    [Fact]
    public void Host_is_case_insensitive() => Assert.NotNull(Pick("https://API.Example.COM/", E("api.EXAMPLE.com")));
}
