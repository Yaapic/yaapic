using System.Text.Json;
using Yaapic.Composition;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

public sealed class Milestone2EndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private static readonly FolderOption TopLevel = new(null, "Top level");

    private readonly LoopbackServer _server;
    private readonly FakeDialogService _dialogs = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-e2e2-" + Guid.NewGuid().ToString("N"));

    public Milestone2EndToEndTests(LoopbackServer server) => _server = server;

    public void Dispose() => TempWorkspace.Delete(_root);

    private async Task<ShellViewModel> NewShellAsync()
    {
        var shell = AppComposition.CreateShell(_root, _dialogs);
        await shell.InitializeAsync();
        return shell;
    }

    private static JsonElement Echo(RequestEditorViewModel tab)
    {
        Assert.Equal(ResponseState.Completed, tab.Response.State);
        return JsonDocument.Parse(tab.Response.BodyText).RootElement;
    }

    private static string EchoHeader(JsonElement echo, string name) => echo.GetProperty("headers").GetProperty(name).GetString()!;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private static CollectionNodeViewModel Find(ShellViewModel shell, string name)
    {
        static IEnumerable<CollectionNodeViewModel> Flatten(IEnumerable<CollectionNodeViewModel> nodes) =>
            nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
        return Flatten(shell.Collections.Roots).Single(n => n.Name == name);
    }

    private string OnDisk(params string[] parts) => Path.Combine([_root, "requests", .. parts]);

    [Fact]
    public async Task PutUrlEncodedAndPatchRawJsonAreEchoed()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Method = HttpMethodKind.Put;
        tab.Url = _server.BaseUrl + "/echo";
        tab.BodyKind = BodyKind.UrlEncoded;
        tab.UrlEncodedFields.Rows[0].Key = "a";
        tab.UrlEncodedFields.Rows[0].Value = "1 2";

        await tab.SendCommand.ExecuteAsync(null);

        var echo = Echo(tab);
        Assert.Equal("PUT", echo.GetProperty("method").GetString());
        Assert.Equal("a=1%202", echo.GetProperty("body").GetString());
        Assert.StartsWith("application/x-www-form-urlencoded", EchoHeader(echo, "Content-Type"));

        tab.Method = HttpMethodKind.Patch;
        tab.BodyKind = BodyKind.Raw;
        tab.BodyContentType = RawContentType.Json;
        tab.BodyText = "{\"b\":2}";

        await tab.SendCommand.ExecuteAsync(null);

        echo = Echo(tab);
        Assert.Equal("PATCH", echo.GetProperty("method").GetString());
        Assert.Equal("{\"b\":2}", echo.GetProperty("body").GetString());
        Assert.StartsWith("application/json", EchoHeader(echo, "Content-Type"));
    }

    [Fact]
    public async Task MultipartAndBinaryBodiesArrive()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "payload.bin");
        byte[] bytes = [0, 1, 2, 250, 251, 252, 255, 10, 13];
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Method = HttpMethodKind.Post;
        tab.Url = _server.BaseUrl + "/echo";
        tab.BodyKind = BodyKind.FormData;
        tab.FormData.Rows[0].Key = "note";
        tab.FormData.Rows[0].Value = "hello";
        tab.FormData.Rows[1].Key = "avatar";
        tab.FormData.Rows[1].Kind = FormFieldKind.File;
        tab.FormData.Rows[1].Value = file;

        await tab.SendCommand.ExecuteAsync(null);

        var echo = Echo(tab);
        Assert.StartsWith("multipart/form-data", EchoHeader(echo, "Content-Type"));
        var body = echo.GetProperty("body").GetString()!;
        Assert.Contains("name=note", body);
        Assert.Contains("hello", body);
        Assert.Contains("name=avatar", body);
        Assert.Contains("payload.bin", body);

        tab.BodyKind = BodyKind.Binary;
        tab.BinaryFilePath = file;

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(bytes, Convert.FromBase64String(Echo(tab).GetProperty("bodyBase64").GetString()!));
    }

    [Fact]
    public async Task BasicAuthAndApiKeyInQueryAreSent()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Url = _server.BaseUrl + "/echo";
        tab.Auth.Kind = AuthKind.Basic;
        tab.Auth.Username = "user";
        tab.Auth.Password = "pass";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal("Basic " + Convert.ToBase64String("user:pass"u8), EchoHeader(Echo(tab), "Authorization"));

        tab.Auth.Kind = AuthKind.ApiKey;
        tab.Auth.Key = "api_key";
        tab.Auth.Value = "s3cret";
        tab.Auth.Location = ApiKeyLocation.Query;

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains("api_key=s3cret", Echo(tab).GetProperty("path").GetString());
    }

    [Fact]
    public async Task RedirectIsFollowedOrReturnedAccordingToTheSetting()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Url = _server.BaseUrl + "/redirect";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.StartsWith("200", tab.Response.StatusText);
        Assert.Equal("/echo", Echo(tab).GetProperty("path").GetString());

        tab.FollowRedirects = false;

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Completed, tab.Response.State);
        Assert.StartsWith("302", tab.Response.StatusText);
        Assert.Contains(tab.Response.Headers, h => h.Key.Equals("Location", StringComparison.OrdinalIgnoreCase) && h.Value.EndsWith("/echo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectionFolderAndRequestLifecycleMatchesTheDisk()
    {
        var shell = await NewShellAsync();
        var tree = shell.Collections;

        _dialogs.PromptAnswers.Enqueue("Acme");
        await tree.NewCollectionCommand.ExecuteAsync(null);
        Assert.True(File.Exists(OnDisk("acme", "folder.yaml")));

        _dialogs.PromptAnswers.Enqueue("Users");
        await tree.NewFolderCommand.ExecuteAsync(Find(shell, "Acme"));
        Assert.True(File.Exists(OnDisk("acme", "users", "folder.yaml")));

        await tree.NewRequestCommand.ExecuteAsync(Find(shell, "Users"));
        Assert.True(File.Exists(OnDisk("acme", "users", "new-request.request.yaml")));

        await tree.MoveCommand.ExecuteAsync(new MoveRequest(Find(shell, "New Request"), TopLevel));
        Assert.False(File.Exists(OnDisk("acme", "users", "new-request.request.yaml")));
        Assert.True(File.Exists(OnDisk("new-request.request.yaml")));

        _dialogs.PromptAnswers.Enqueue("Renamed");
        await tree.RenameRequestCommand.ExecuteAsync(Find(shell, "New Request"));
        Assert.False(File.Exists(OnDisk("new-request.request.yaml")));
        Assert.True(File.Exists(OnDisk("renamed.request.yaml")));

        _dialogs.DeleteAnswer = true;
        await tree.DeleteRequestCommand.ExecuteAsync(Find(shell, "Renamed"));
        Assert.False(File.Exists(OnDisk("renamed.request.yaml")));
        Assert.True(File.Exists(OnDisk("acme", "users", "folder.yaml")));
    }

    [Fact]
    public async Task VariableScopesResolveEnvironmentThenCollectionThenGlobals()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, "globals.yaml"),
            "version: 1\nvariables:\n- key: name\n  value: glob\n  enabled: true\n", TestContext.Current.CancellationToken);
        var shell = await NewShellAsync();
        var tree = shell.Collections;

        _dialogs.PromptAnswers.Enqueue("Acme");
        await tree.NewCollectionCommand.ExecuteAsync(null);
        tree.EditSettingsCommand.Execute(Find(shell, "Acme"));
        var settings = tree.SettingsEditor!;
        settings.Variables.Rows[0].Key = "name";
        settings.Variables.Rows[0].Value = "col";
        await settings.SaveCommand.ExecuteAsync(null);
        await tree.RefreshAsync();
        await tree.NewRequestCommand.ExecuteAsync(Find(shell, "Acme"));

        shell.Environments.AddCommand.Execute(null);
        var environment = shell.Environments.EditedEnvironment!;
        environment.Variables.Rows[0].Key = "name";
        environment.Variables.Rows[0].Value = "env";
        shell.Environments.ActiveEnvironment = environment;

        var tab = shell.Tabs.ActiveTab!;
        tab.Url = _server.BaseUrl + "/echo?v={{name}}";

        await tab.SendCommand.ExecuteAsync(null);
        Assert.EndsWith("v=env", Echo(tab).GetProperty("path").GetString());

        shell.Environments.ActiveEnvironment = null;
        await tab.SendCommand.ExecuteAsync(null);
        Assert.EndsWith("v=col", Echo(tab).GetProperty("path").GetString());

        await tree.MoveCommand.ExecuteAsync(new MoveRequest(Find(shell, "New Request"), TopLevel));
        await tab.SendCommand.ExecuteAsync(null);
        Assert.EndsWith("v=glob", Echo(tab).GetProperty("path").GetString());
    }

    [Fact]
    public async Task HistoryIsRecordedReopenedClearedAndSurvivesARestart()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        var recorded = 0;
        foreach (var path in new[] { "/echo/1", "/echo/2", "/echo/3" })
        {
            tab.Url = _server.BaseUrl + path;
            await tab.SendCommand.ExecuteAsync(null);
            recorded++;
            await WaitUntilAsync(() => shell.History.Groups.SelectMany(g => g.Items).Count() == recorded);
        }

        var items = shell.History.Groups.SelectMany(g => g.Items).ToList();
        Assert.EndsWith("/3", items[0].Url);
        Assert.EndsWith("/1", items[2].Url);

        var tabCount = shell.Tabs.Tabs.Count;
        shell.History.OpenCommand.Execute(items[2]);
        Assert.Equal(tabCount + 1, shell.Tabs.Tabs.Count);
        Assert.EndsWith("/1", shell.Tabs.ActiveTab!.Url);

        var restarted = await NewShellAsync();
        Assert.Equal(3, restarted.History.Groups.SelectMany(g => g.Items).Count());

        _dialogs.DeleteAnswer = true;
        await restarted.History.ClearCommand.ExecuteAsync(null);
        Assert.Empty(restarted.History.Groups);
        var file = Path.Combine(_root, ".yaapic", "history.jsonl");
        Assert.True(!File.Exists(file) || new FileInfo(file).Length == 0);
    }

    [Fact]
    public async Task PaletteFindsARequestByNameAndOpensItsTab()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Name = "Quarterly Report";
        tab.Url = _server.BaseUrl + "/echo";
        Assert.True(await shell.Tabs.SaveAsync(tab));
        await shell.Collections.RefreshAsync();
        shell.Tabs.CloseWithoutPrompt(tab.Id);
        Assert.DoesNotContain(shell.Tabs.Tabs, t => t.Id == tab.Id);

        shell.Palette.OpenCommand.Execute(null);
        shell.Palette.Query = "quarterly";

        var first = shell.Palette.Results[0];
        Assert.Equal("Quarterly Report", first.Item.Title);

        await shell.Palette.ExecuteCommand.ExecuteAsync(first);

        Assert.Contains(shell.Tabs.Tabs, t => t.Id == tab.Id);
    }
}
