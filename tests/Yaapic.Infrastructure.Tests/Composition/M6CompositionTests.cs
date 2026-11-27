using System.Net;
using System.Net.Sockets;
using Yaapic.Composition;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Infrastructure.Tests.EndToEnd;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class M6CompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private readonly string _data = Path.Combine(Path.GetTempPath(), "yaapic-data-" + Guid.NewGuid().ToString("N"));
    private readonly LoopbackServer _server = new();

    public void Dispose()
    {
        _server.Dispose();
        TempWorkspace.Delete(_root);
        TempWorkspace.Delete(_data);
    }

    private async Task<ShellViewModel> CreateAsync()
    {
        var shell = AppComposition.CreateShell(_root, new FakeDialogService(), null, new DefaultVaultLocation(_data), [], userDataDirectory: _data);
        await shell.InitializeAsync();
        return shell;
    }

    private void WriteFolder()
    {
        Directory.CreateDirectory(Path.Combine(_root, "requests", "Pets"));
        File.WriteAllText(
            Path.Combine(_root, "requests", "Pets", "folder.yaml"),
            $"version: 1\nid: {Guid.NewGuid():D}\nname: Pets\ndescription: \"\"\nvariables: []\n");
        foreach (var name in new[] { "One", "Two" })
        {
            File.WriteAllText(
                Path.Combine(_root, "requests", "Pets", name.ToLowerInvariant() + ".request.yaml"),
                $"version: 1\nid: {Guid.NewGuid():D}\nname: {name}\nmethod: GET\nurl: \"{_server.BaseUrl}/{name.ToLowerInvariant()}\"\nparams: []\nheaders: []\n");
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task TheShellCarriesTheFourToolsAndTheirPaletteEntries()
    {
        var shell = await CreateAsync();

        Assert.True(shell.CanAutomate);
        Assert.NotNull(shell.Runner);
        Assert.NotNull(shell.LoadTest);
        Assert.NotNull(shell.Mocks);
        Assert.NotNull(shell.Monitors);

        shell.Palette.OpenCommand.Execute(null);
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:run-collection");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:monitors");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:mock-servers");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:load-test");
    }

    [Fact]
    public async Task TheRunnerRunsAFolderAgainstTheLoopbackServer()
    {
        WriteFolder();
        var shell = await CreateAsync();

        var runner = shell.Runner!;
        runner.Open(shell.Collections.Roots.Single().Node);
        await runner.RunCommand.ExecuteAsync(null);

        Assert.Equal(2, runner.Rows.Count);
        Assert.Equal(2, _server.Hits.Count);
    }

    [Fact]
    public async Task AMockServerAnswersAndClosingTheShellFreesItsPort()
    {
        var shell = await CreateAsync();
        var mocks = shell.Mocks!;
        await mocks.NewServerCommand.ExecuteAsync(null);
        var port = FreePort();
        mocks.Port = port;
        await mocks.SaveCommand.ExecuteAsync(null);
        await mocks.StartCommand.ExecuteAsync(null);
        Assert.Null(mocks.Error);

        using var client = new HttpClient();
        using var response = await client.GetAsync($"http://127.0.0.1:{port}/anything", TestContext.Current.CancellationToken);
        Assert.NotEqual(0, (int)response.StatusCode);

        await shell.RequestCloseAsync();

        using var probe = new TcpListener(IPAddress.Loopback, port);
        probe.Start();
    }

    [Fact]
    public async Task AMonitorRunNowRecordsARun()
    {
        WriteFolder();
        var shell = await CreateAsync();
        var monitors = shell.Monitors!;
        await monitors.LoadAsync();
        await monitors.NewMonitorCommand.ExecuteAsync(null);
        monitors.RunNowCommand.Execute(null);

        var file = Path.Combine(_root, ".yaapic", "monitor-runs.jsonl");
        await WaitAsync(() => File.Exists(file) && new FileInfo(file).Length > 0);
        await shell.RequestCloseAsync();
    }
}
