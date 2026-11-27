using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Mocks;

namespace Yaapic.ViewModels.Tests.Mocks;

public sealed class MockServersViewModelTests
{
    private readonly InMemoryMockServerStore _store = new();
    private readonly FakeMockHttpHost _host = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly MockServerService _service;
    private readonly MockServersViewModel _vm;

    public MockServersViewModelTests()
    {
        _service = new MockServerService(_store, _host, new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        _vm = new MockServersViewModel(_service, _dialogs);
    }

    private static MockServerDefinition Server(string name, int port) =>
        MockServerDefinition.Create(name, port) with { Routes = [MockRoute.Create("hello") with { Path = "/hello", Body = "hi" }] };

    private async Task<MockServerDefinition> StoredAsync(string name, int port)
    {
        var s = Server(name, port);
        await _store.SaveAsync(s, CancellationToken.None);
        return s;
    }

    private async Task LoadedWithAsync(params MockServerDefinition[] servers)
    {
        foreach (var s in servers)
        {
            await _store.SaveAsync(s, CancellationToken.None);
        }

        await _vm.LoadAsync();
    }

    [Fact]
    public async Task Load_lists_the_servers_and_selects_the_first()
    {
        await LoadedWithAsync(Server("a", 4000), Server("b", 4001));

        Assert.Equal(["a", "b"], _vm.Servers.Select(s => s.Name));
        Assert.Equal("a", _vm.SelectedServer?.Name);
        Assert.Equal("a", _vm.Name);
        Assert.Equal(4000, _vm.Port);
        Assert.Single(_vm.Routes);
        Assert.False(_vm.IsDirty);
        Assert.Equal("http://127.0.0.1:4000", _vm.SelectedServer!.BaseUrl);
        Assert.Equal(Strings.MockStopped, _vm.SelectedServer.StatusText);
    }

    [Fact]
    public async Task NewServer_saves_at_once_and_selects_it()
    {
        await _vm.LoadAsync();

        await _vm.NewServerCommand.ExecuteAsync(null);

        Assert.Equal(Strings.MockNewName, _vm.SelectedServer?.Name);
        Assert.Equal(Strings.MockNewName, (await _store.ListAsync(CancellationToken.None)).Single().Name);
    }

    [Fact]
    public async Task Edit_then_save_stores_the_change()
    {
        await LoadedWithAsync(Server("a", 4000));
        Assert.False(_vm.SaveCommand.CanExecute(null));

        _vm.Name = "renamed";
        _vm.Routes[0].Status = 201;
        _vm.Routes[0].Headers.Rows[^1].Key = "X-Test";
        Assert.True(_vm.IsDirty);
        Assert.True(_vm.SaveCommand.CanExecute(null));
        await _vm.SaveCommand.ExecuteAsync(null);

        var stored = (await _store.ListAsync(CancellationToken.None)).Single();
        Assert.Equal("renamed", stored.Name);
        Assert.Equal(201, stored.Routes[0].Status);
        Assert.Contains(stored.Routes[0].Headers, h => h.Key == "X-Test");
        Assert.False(_vm.IsDirty);
        Assert.Equal("renamed", _vm.SelectedServer!.Name);
    }

    [Fact]
    public async Task Save_with_issues_shows_them_and_stores_nothing()
    {
        await LoadedWithAsync(Server("a", 4000));
        _store.ResetSaveCount();

        _vm.Port = 80;
        _vm.Routes[0].Path = "no-slash";
        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal([Strings.MockIssuePortOutOfRange, Strings.MockIssueRoutePathInvalid], _vm.ValidationErrors);
        Assert.Equal(Strings.MockIssueRoutePathInvalid, _vm.Routes[0].IssueText);
        Assert.Equal(0, _store.SaveCount);
        Assert.True(_vm.IsDirty);
    }

    [Fact]
    public async Task Revert_restores_the_saved_values()
    {
        await LoadedWithAsync(Server("a", 4000));
        _vm.Name = "x";
        _vm.Routes[0].Body = "changed";

        _vm.RevertCommand.Execute(null);

        Assert.Equal("a", _vm.Name);
        Assert.Equal("hi", _vm.Routes[0].Body);
        Assert.False(_vm.IsDirty);
    }

    [Theory]
    [InlineData(UnsavedChoice.Save, true)]
    [InlineData(UnsavedChoice.Discard, true)]
    [InlineData(UnsavedChoice.Cancel, false)]
    public async Task Changing_selection_with_edits_asks_first(UnsavedChoice answer, bool switched)
    {
        await LoadedWithAsync(Server("a", 4000), Server("b", 4001));
        var first = _vm.Servers[0];
        var second = _vm.Servers[1];
        _vm.Name = "edited";
        _dialogs.UnsavedAnswer = answer;

        var result = await _vm.SelectServerAsync(second);

        Assert.Equal(switched, result);
        Assert.Equal(["edited"], _dialogs.UnsavedPrompts);
        Assert.Equal(switched ? second : first, _vm.SelectedServer);
        Assert.Equal(switched ? "b" : "edited", _vm.Name);
        var stored = await _store.ListAsync(CancellationToken.None);
        Assert.Equal(answer == UnsavedChoice.Save, stored.Any(s => s.Name == "edited"));
    }

    [Fact]
    public async Task Selection_without_edits_does_not_ask()
    {
        await LoadedWithAsync(Server("a", 4000), Server("b", 4001));

        await _vm.SelectServerAsync(_vm.Servers[1]);

        Assert.Empty(_dialogs.UnsavedPrompts);
        Assert.Equal("b", _vm.Name);
    }

    [Fact]
    public async Task Delete_asks_for_confirmation()
    {
        await LoadedWithAsync(Server("a", 4000));

        await _vm.DeleteServerCommand.ExecuteAsync(null);
        Assert.Single(_vm.Servers);

        _dialogs.DeleteAnswer = true;
        await _vm.DeleteServerCommand.ExecuteAsync(null);

        Assert.Equal(["a", "a"], _dialogs.DeletePrompts);
        Assert.Empty(_vm.Servers);
        Assert.Null(_vm.SelectedServer);
        Assert.Empty(await _store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Start_and_stop_update_the_state_and_the_status_bar()
    {
        await LoadedWithAsync(Server("a", 4000));
        Assert.Equal("", _vm.RunningText);

        await _vm.StartCommand.ExecuteAsync(null);

        Assert.True(_vm.SelectedIsRunning);
        Assert.Equal(Strings.MockRunning, _vm.SelectedServer!.StatusText);
        Assert.Equal(1, _vm.RunningCount);
        Assert.Equal(Strings.MockStatusBar(1), _vm.RunningText);
        Assert.Contains(4000, _host.ListeningPorts);

        await _vm.StopCommand.ExecuteAsync(null);

        Assert.False(_vm.SelectedIsRunning);
        Assert.Equal("", _vm.RunningText);
        Assert.Empty(_host.ListeningPorts);
    }

    [Fact]
    public async Task Start_on_a_port_in_use_sets_the_error()
    {
        await LoadedWithAsync(Server("a", 4000));
        _host.PortsInUse.Add(4000);

        await _vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(Strings.MockPortInUse(4000), _vm.Error);
        Assert.False(_vm.SelectedIsRunning);
    }

    [Fact]
    public async Task Routes_can_be_added_duplicated_moved_and_deleted()
    {
        await LoadedWithAsync(Server("a", 4000));

        _vm.AddRouteCommand.Execute(null);
        Assert.Equal(2, _vm.Routes.Count);
        Assert.Same(_vm.Routes[1], _vm.SelectedRoute);
        Assert.True(_vm.IsDirty);

        _vm.MoveRouteUpCommand.Execute(null);
        Assert.Equal(Strings.MockNewRoute, _vm.Routes[0].Name);
        _vm.MoveRouteUpCommand.Execute(null);
        Assert.Equal(Strings.MockNewRoute, _vm.Routes[0].Name);
        _vm.MoveRouteDownCommand.Execute(null);
        Assert.Equal("hello", _vm.Routes[0].Name);

        _vm.SelectedRoute = _vm.Routes[0];
        _vm.DuplicateRouteCommand.Execute(null);
        Assert.Equal(3, _vm.Routes.Count);
        Assert.Equal(Strings.CommonCopyName("hello"), _vm.Routes[1].Name);
        Assert.NotEqual(_vm.Routes[0].Id, _vm.Routes[1].Id);
        Assert.Equal("hi", _vm.Routes[1].Body);

        _vm.DeleteRouteCommand.Execute(null);
        Assert.Equal(2, _vm.Routes.Count);
        Assert.NotNull(_vm.SelectedRoute);

        _vm.RevertCommand.Execute(null);
        Assert.False(_vm.IsDirty);
        Assert.Single(_vm.Routes);
    }

    private static MockHttpRequest Post(string target, string body) =>
        MockHttpRequest.Parse("POST", target, [new KeyValueEntry("X-Id", "7")], Encoding.UTF8.GetBytes(body), "HTTP/1.1");

    [Fact]
    public async Task A_request_appears_in_the_log_with_its_route_and_details()
    {
        await LoadedWithAsync(Server("a", 4000) with
        {
            Routes = [MockRoute.Create("hello") with { Method = "ANY", Path = "/hello" }],
        });
        await _vm.StartCommand.ExecuteAsync(null);

        await _host.SendAsync(4000, Post("/hello?x=1", "payload"));
        await _host.SendAsync(4000, Post("/other", ""));

        Assert.Equal(2, _vm.Log.Count);
        Assert.Equal(Strings.MockNoMatch, _vm.Log[0].Route);
        Assert.Equal(404, _vm.Log[0].Status);
        var row = _vm.Log[1];
        Assert.Equal("hello", row.Route);
        Assert.Equal("POST", row.Method);
        Assert.Equal("/hello?x=1", row.Target);
        Assert.Equal(200, row.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), row.Time);

        _vm.SelectedLog = row;
        Assert.Equal("payload", _vm.SelectedLogBody);
        Assert.Contains(_vm.SelectedLogHeaders, h => h.Key == "X-Id" && h.Value == "7");
    }

    [Fact]
    public async Task Requests_from_concurrent_connections_all_get_an_answer_and_a_log_row()
    {
        await LoadedWithAsync(Server("a", 4000) with { Routes = [MockRoute.Create("hello") with { Method = "ANY", Path = "/hello" }] });
        await _vm.StartCommand.ExecuteAsync(null);

        // The host calls the handler from one thread per connection; with no UI context the log is updated on those threads.
        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            var statuses = new List<int>();
            for (var i = 0; i < 100; i++)
            {
                statuses.Add((await _host.SendAsync(4000, Post("/hello", ""))).Status);
            }

            return statuses;
        }, TestContext.Current.CancellationToken)));

        Assert.All(answers.SelectMany(a => a), status => Assert.Equal(200, status));
        Assert.Equal(500, _vm.Log.Count);
        Assert.All(_vm.Log, row => Assert.NotNull(row));
    }

    [Fact]
    public async Task ClearLog_empties_the_log()
    {
        await LoadedWithAsync(Server("a", 4000));
        await _vm.StartCommand.ExecuteAsync(null);
        await _host.SendAsync(4000, Post("/hello", ""));
        Assert.Single(_vm.Log);

        _vm.ClearLogCommand.Execute(null);

        Assert.Empty(_vm.Log);
        Assert.Empty(_service.GetLog(_vm.SelectedServer!.Id));
    }

    [Fact]
    public async Task CopyUrl_puts_the_base_url_on_the_clipboard()
    {
        await LoadedWithAsync(Server("a", 4000));

        await _vm.CopyUrlCommand.ExecuteAsync(null);

        Assert.Equal(["http://127.0.0.1:4000"], _dialogs.ClipboardTexts);
    }

    [Fact]
    public async Task GenerateFromFolder_saves_selects_and_reports()
    {
        await _vm.LoadAsync();
        var folder = CollectionNode.ForFolder(FolderDefinition.Create("Shop"),
        [
            CollectionNode.ForRequest(HttpRequestDefinition.Create("List") with { Url = "{{baseUrl}}/items" }),
            CollectionNode.ForRequest(HttpRequestDefinition.Create("Again") with { Url = "{{baseUrl}}/items" }),
        ]);

        await _vm.GenerateFromFolderAsync(folder);

        Assert.Equal(Strings.MockGeneratedName("Shop"), _vm.SelectedServer?.Name);
        Assert.Single(_vm.Routes);
        Assert.Equal("/items", _vm.Routes[0].Path);
        Assert.Equal(Strings.MockGenerated(1, 1), _vm.GenerationMessage);
        Assert.Single(await _store.ListAsync(CancellationToken.None));
    }
}
