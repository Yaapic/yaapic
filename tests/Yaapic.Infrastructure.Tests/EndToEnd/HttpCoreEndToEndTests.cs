using Yaapic.Composition;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

public sealed class HttpCoreEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private readonly LoopbackServer _server;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-e2e-" + Guid.NewGuid().ToString("N"));

    public HttpCoreEndToEndTests(LoopbackServer server) => _server = server;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<ShellViewModel> NewShellAsync()
    {
        var shell = AppComposition.CreateShell(_root, new FakeDialogService());
        await shell.InitializeAsync();
        return shell;
    }

    [Fact]
    public async Task GetWithQueryAndHeaderIsEchoedAndPrettyPrinted()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Url = _server.BaseUrl + "/echo";
        tab.QueryParams.Rows[0].Key = "q";
        tab.QueryParams.Rows[0].Value = "1";
        tab.Headers.Rows[0].Key = "X-Test";
        tab.Headers.Rows[0].Value = "abc";

        await tab.SendCommand.ExecuteAsync(null);

        var response = tab.Response;
        Assert.Equal(ResponseState.Completed, response.State);
        Assert.StartsWith("200", response.StatusText);
        Assert.Contains("/echo?q=1", response.BodyText);
        Assert.Contains("\"X-Test\": \"abc\"", response.BodyText);
        Assert.Contains("\n  \"method\": \"GET\"", response.BodyText);
    }

    [Fact]
    public async Task PostRawJsonBodyIsEchoedWithItsContentType()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Method = HttpMethodKind.Post;
        tab.Url = _server.BaseUrl + "/echo";
        tab.BodyKind = BodyKind.Raw;
        tab.BodyContentType = RawContentType.Json;
        tab.BodyText = "{\"a\":1}";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Completed, tab.Response.State);
        Assert.Contains("\"method\": \"POST\"", tab.Response.BodyText);
        Assert.Contains("{\\\"a\\\":1}", tab.Response.BodyText);
        Assert.Contains("\"Content-Type\": \"application/json", tab.Response.BodyText);
    }

    [Fact]
    public async Task EnvironmentVariableIsResolvedAndMissingOneIsReported()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Url = "{{baseUrl}}/echo";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Failed, tab.Response.State);
        Assert.Contains("baseUrl", tab.Response.UnresolvedWarning);

        shell.Environments.AddCommand.Execute(null);
        var environment = shell.Environments.EditedEnvironment!;
        environment.Variables.Rows[0].Key = "baseUrl";
        environment.Variables.Rows[0].Value = _server.BaseUrl;
        shell.Environments.ActiveEnvironment = environment;

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Completed, tab.Response.State);
        Assert.Null(tab.Response.UnresolvedWarning);
        Assert.Contains("\"path\": \"/echo\"", tab.Response.BodyText);
    }

    [Fact]
    public async Task ServerErrorIsCategorizedAndSlowRequestCanBeCanceled()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Url = _server.BaseUrl + "/status/500";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(StatusCategory.ServerError, tab.Response.StatusCategory);

        tab.Url = _server.BaseUrl + "/delay/2000";
        var send = tab.SendCommand.ExecuteAsync(null);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        tab.CancelCommand.Execute(null);
        await send;

        Assert.Equal(ResponseState.Failed, tab.Response.State);
        Assert.Equal("Request canceled", tab.Response.ErrorMessage);
    }

    [Fact]
    public async Task SavedRequestAndItsTabAreRestoredByANewShell()
    {
        var shell = await NewShellAsync();
        var tab = shell.Tabs.Tabs[0];
        tab.Name = "Persisted";
        tab.Url = _server.BaseUrl + "/echo";
        Assert.True(await shell.Tabs.SaveAsync(tab));
        Assert.True(await shell.RequestCloseAsync());

        var restored = await NewShellAsync();

        Assert.Contains(restored.Requests.Items, i => i.Id == tab.Id && i.Name == "Persisted");
        var restoredTab = Assert.Single(restored.Tabs.Tabs);
        Assert.Equal(tab.Id, restoredTab.Id);
        Assert.Same(restoredTab, restored.Tabs.ActiveTab);
    }
}
