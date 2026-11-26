using Yaapic.Domain.Mocks;

namespace Yaapic.Domain.Tests.Mocks;

public class MockServerTests
{
    private static MockServerDefinition WithRoute(Func<MockRoute, MockRoute> change, out Guid id)
    {
        var r = change(MockRoute.Create("r"));
        id = r.Id;
        return MockServerDefinition.Create("s") with { Routes = [r] };
    }

    [Fact]
    public void Create_defaults()
    {
        var s = MockServerDefinition.Create("s");
        Assert.True(s.Cors);
        Assert.Equal(3000, s.Port);
        Assert.Empty(s.Routes);
        var r = MockRoute.Create("r");
        Assert.True(r.Enabled);
        Assert.Equal(("GET", "/", 200, "{}", 0), (r.Method, r.Path, r.Status, r.Body, r.DelayMs));
        Assert.Equal("Content-Type", Assert.Single(r.Headers).Key);
        Assert.Empty(MockServerValidator.Validate(s with { Routes = [r] }));
    }

    [Fact]
    public void Server_level_issues()
    {
        var s = MockServerDefinition.Create(" ", 80);
        var issues = MockServerValidator.Validate(s);
        Assert.Equal([MockIssue.NameEmpty, MockIssue.PortOutOfRange], issues.Select(i => i.Issue));
        Assert.All(issues, i => Assert.Null(i.RouteId));
        Assert.Contains(MockServerValidator.Validate(s with { Name = "a", Port = 70000 }), i => i.Issue == MockIssue.PortOutOfRange);
    }

    public static TheoryData<Func<MockRoute, MockRoute>, MockIssue> Bad => new()
    {
        { r => r with { Method = "get" }, MockIssue.RouteMethodInvalid },
        { r => r with { Method = "TRACE" }, MockIssue.RouteMethodInvalid },
        { r => r with { Path = "x" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a?b" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a#b" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a b" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a\u0001" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a/*/b" }, MockIssue.RoutePathInvalid },
        { r => r with { Path = "/a*" }, MockIssue.RoutePathInvalid },
        { r => r with { Status = 99 }, MockIssue.RouteStatusOutOfRange },
        { r => r with { Status = 600 }, MockIssue.RouteStatusOutOfRange },
        { r => r with { DelayMs = -1 }, MockIssue.RouteDelayOutOfRange },
        { r => r with { DelayMs = 60001 }, MockIssue.RouteDelayOutOfRange },
        { r => r with { Headers = [new("Bad Name", "v")] }, MockIssue.RouteHeaderNameInvalid },
        { r => r with { Headers = [new("", "v")] }, MockIssue.RouteHeaderNameInvalid },
    };

    [Theory]
    [MemberData(nameof(Bad))]
    public void Route_issue_carries_route_id(Func<MockRoute, MockRoute> change, MockIssue expected)
    {
        var s = WithRoute(change, out var id);
        var issue = Assert.Single(MockServerValidator.Validate(s));
        Assert.Equal(new MockValidationIssue(expected, id), issue);
    }

    [Theory]
    [InlineData("/*")]
    [InlineData("/files/*")]
    [InlineData("/users/:id")]
    [InlineData("/users/{id}/posts")]
    public void Valid_paths(string path) =>
        Assert.Empty(MockServerValidator.Validate(WithRoute(r => r with { Path = path }, out _)));

    [Fact]
    public void Disabled_header_is_not_checked() =>
        Assert.Empty(MockServerValidator.Validate(WithRoute(r => r with { Headers = [new("bad name", "v", false)] }, out _)));

    [Fact]
    public void IsEquivalentTo_compares_routes_and_headers()
    {
        var a = WithRoute(r => r, out _);
        var route = a.Routes[0];
        Assert.True(a.IsEquivalentTo(a with { Routes = [route with { Headers = [.. route.Headers] }] }));
        Assert.False(a.IsEquivalentTo(a with { Routes = [route with { Headers = [new("X", "1")] }] }));
        Assert.False(a.IsEquivalentTo(a with { Routes = [] }));
        Assert.False(a.IsEquivalentTo(a with { Port = 3001 }));
    }
}
