using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Mocks;

public class MockServerServiceTests
{
    private readonly InMemoryMockServerStore _store = new();
    private readonly FakeMockHttpHost _host = new();
    private readonly ManualTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private readonly MockServerService _service;

    public MockServerServiceTests() => _service = new MockServerService(_store, _host, _time);

    private static MockHttpRequest Get(string target) =>
        MockHttpRequest.Parse("GET", target, [], [], "HTTP/1.1");

    private static MockServerDefinition Server(string name, int port, string body = "{}") =>
        MockServerDefinition.Create(name, port) with
        {
            Routes = [MockRoute.Create("hello") with { Path = "/hello", Body = body }],
        };

    private async Task<MockServerDefinition> SavedAsync(string name = "a", int port = 4000, string body = "{}")
    {
        var s = Server(name, port, body);
        await _service.SaveAsync(s, CancellationToken.None);
        return s;
    }

    [Fact]
    public async Task Load_lists_the_stored_servers_and_raises_the_event()
    {
        var a = Server("a", 4000);
        await _store.SaveAsync(a, CancellationToken.None);
        var changed = 0;
        _service.ServersChanged += (_, _) => changed++;

        await _service.LoadAsync(CancellationToken.None);

        Assert.Equal([a], _service.Servers);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Save_with_issues_returns_them_and_stores_nothing()
    {
        var bad = Server("a", 80);

        var issues = await _service.SaveAsync(bad, CancellationToken.None);

        Assert.Contains(issues, i => i.Issue == MockIssue.PortOutOfRange);
        Assert.Equal(0, _store.SaveCount);
        Assert.Empty(_service.Servers);
    }

    [Fact]
    public async Task Save_of_a_running_server_is_used_by_the_next_request()
    {
        var s = await SavedAsync(body: "old");
        await _service.StartAsync(s.Id, CancellationToken.None);

        await _service.SaveAsync(s with { Routes = [s.Routes[0] with { Body = "new" }] }, CancellationToken.None);
        var response = await _host.SendAsync(4000, Get("/hello"));

        Assert.Equal("new", Encoding.UTF8.GetString(response.Body));
        Assert.Equal([4000], _host.StartedPorts);
    }

    [Fact]
    public async Task Save_with_a_new_port_restarts_the_running_server()
    {
        var s = await SavedAsync();
        await _service.StartAsync(s.Id, CancellationToken.None);

        await _service.SaveAsync(s with { Port = 4001 }, CancellationToken.None);

        Assert.Equal([4000, 4001], _host.StartedPorts);
        Assert.Equal([4001], _host.ListeningPorts);
        Assert.True(_service.IsRunning(s.Id));
    }

    [Fact]
    public async Task Save_with_a_port_in_use_stops_the_server_and_throws()
    {
        var s = await SavedAsync();
        await _service.StartAsync(s.Id, CancellationToken.None);
        _host.PortsInUse.Add(4001);

        await Assert.ThrowsAsync<MockPortInUseException>(() => _service.SaveAsync(s with { Port = 4001 }, CancellationToken.None));

        Assert.False(_service.IsRunning(s.Id));
    }

    [Fact]
    public async Task Start_stop_and_events()
    {
        var s = await SavedAsync();
        var raised = new List<Guid>();
        _service.RunningChanged += (_, id) => raised.Add(id);

        await _service.StartAsync(s.Id, CancellationToken.None);
        await _service.StartAsync(s.Id, CancellationToken.None);
        Assert.True(_service.IsRunning(s.Id));
        Assert.Equal(1, _service.RunningCount);

        await _service.StopAsync(s.Id);
        await _service.StopAsync(s.Id);
        Assert.False(_service.IsRunning(s.Id));
        Assert.Equal(0, _service.RunningCount);
        Assert.Equal([s.Id, s.Id], raised);
    }

    [Fact]
    public async Task Start_of_an_invalid_server_throws()
    {
        var bad = Server("a", 80);
        await _store.SaveAsync(bad, CancellationToken.None);
        await _service.LoadAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartAsync(bad.Id, CancellationToken.None));

        Assert.Empty(_host.StartedPorts);
    }

    [Fact]
    public async Task Start_on_a_port_used_by_a_running_server_throws_before_calling_the_host()
    {
        var a = await SavedAsync("a");
        var b = await SavedAsync("b");
        await _service.StartAsync(a.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<MockPortInUseException>(() => _service.StartAsync(b.Id, CancellationToken.None));

        Assert.Equal(4000, ex.Port);
        Assert.Equal([4000], _host.StartedPorts);
    }

    [Fact]
    public async Task Start_passes_the_host_port_in_use_error_through()
    {
        var s = await SavedAsync();
        _host.PortsInUse.Add(4000);

        await Assert.ThrowsAsync<MockPortInUseException>(() => _service.StartAsync(s.Id, CancellationToken.None));

        Assert.False(_service.IsRunning(s.Id));
    }

    [Fact]
    public async Task Delete_stops_and_removes_the_server_and_its_log()
    {
        var s = await SavedAsync();
        await _service.StartAsync(s.Id, CancellationToken.None);
        await _host.SendAsync(4000, Get("/hello"));

        await _service.DeleteAsync(s.Id, CancellationToken.None);

        Assert.False(_service.IsRunning(s.Id));
        Assert.Empty(_service.Servers);
        Assert.Empty(_service.GetLog(s.Id));
        Assert.Empty(await _store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Log_records_route_name_or_null_and_raises_the_event()
    {
        var s = await SavedAsync();
        await _service.StartAsync(s.Id, CancellationToken.None);
        var logged = new List<MockLogged>();
        _service.RequestLogged += (_, e) => logged.Add(e);

        await _host.SendAsync(4000, Get("/hello"));
        await _host.SendAsync(4000, Get("/nope"));

        var log = _service.GetLog(s.Id);
        Assert.Equal(["/nope", "/hello"], log.Select(e => e.Target));
        Assert.Equal(["hello", null], log.Reverse().Select(e => e.RouteName));
        Assert.Equal([404, 200], log.Select(e => e.Status));
        Assert.Equal(2, logged.Count);
        Assert.All(logged, e => Assert.Equal(s.Id, e.ServerId));
    }

    [Fact]
    public async Task Log_keeps_the_500_newest_entries_and_can_be_cleared()
    {
        var s = await SavedAsync();
        await _service.StartAsync(s.Id, CancellationToken.None);
        for (var i = 0; i < 510; i++)
        {
            await _host.SendAsync(4000, Get("/r" + i));
        }

        var log = _service.GetLog(s.Id);
        Assert.Equal(500, log.Count);
        Assert.Equal("/r509", log[0].Target);
        Assert.Equal("/r10", log[^1].Target);

        _service.ClearLog(s.Id);
        Assert.Empty(_service.GetLog(s.Id));
    }

    [Fact]
    public async Task StopAll_stops_every_server_and_dispose_does_the_same()
    {
        var a = await SavedAsync("a", 4000);
        var b = await SavedAsync("b", 4001);
        await _service.StartAsync(a.Id, CancellationToken.None);
        await _service.StartAsync(b.Id, CancellationToken.None);

        await _service.StopAllAsync();
        Assert.Equal(0, _service.RunningCount);

        await _service.StartAsync(a.Id, CancellationToken.None);
        await _service.DisposeAsync();
        Assert.Equal(0, _service.RunningCount);
        Assert.Empty(_host.ListeningPorts);
    }
}
