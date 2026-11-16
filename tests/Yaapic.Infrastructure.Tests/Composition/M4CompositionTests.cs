using System.Text;
using Yaapic.Composition;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Tests.EndToEnd;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class M4CompositionTests : IDisposable
{
    private const string TokenUrl = "https://auth.example.test/token";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private readonly string _data = Path.Combine(Path.GetTempPath(), "yaapic-data-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        TempWorkspace.Delete(_root);
        TempWorkspace.Delete(_data);
    }

    private ShellViewModel Create(FakeHttpTransport? transport = null) =>
        AppComposition.CreateShell(_root, new FakeDialogService(), null, new DefaultVaultLocation(_data), [], userDataDirectory: _data, transport: transport);

    private static Task<SendResult> Ok(string body) => Task.FromResult(new SendResult(
        new HttpResponseData(200, "OK", [new KeyValueEntry("Content-Type", "application/json")], Encoding.UTF8.GetBytes(body), TimeSpan.Zero, ""), null));

    [Fact]
    public async Task AnOAuth2RequestThroughTheComposedSenderReachesTheTokenService()
    {
        var transport = new FakeHttpTransport((r, _) => Ok(r.Url == TokenUrl ? """{"access_token":"tok-1","token_type":"Bearer","expires_in":3600}""" : "{}"));
        var shell = Create(transport);
        await shell.InitializeAsync();
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = "https://api.example.test/me";
        tab.Auth.Load(RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = OAuth2Settings.Default with { TokenUrl = TokenUrl, ClientId = "id", ClientSecret = "secret" } });

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(TokenUrl, transport.Requests[0].Url);
        var api = transport.Requests[^1];
        Assert.Equal("https://api.example.test/me", api.Url);
        Assert.Contains(api.Headers, h => h.Key == "Authorization" && h.Value == "Bearer tok-1");
    }

    [Fact]
    public async Task ThePaletteListsTheM4Commands()
    {
        var shell = Create();
        await shell.InitializeAsync();

        shell.Palette.OpenCommand.Execute(null);

        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:decode-jwt");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:client-certificates");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:clear-oauth-tokens");
        Assert.Contains(shell.Palette.Results, r => r.Item.Id == "command:generate-code");
    }

    [Fact]
    public async Task TheShellCreatesTheToolViewModels()
    {
        var shell = Create();
        await shell.InitializeAsync();

        Assert.NotNull(shell.CreateCertificates());
        Assert.NotNull(shell.CreateJwtDecoder());
    }

    [Fact]
    public async Task TheCodeGenerationDialogOffersTheSixGeneratorsInOrder()
    {
        var shell = Create();
        await shell.InitializeAsync();
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = "https://api.example.test/me";

        var dialog = tab.CreateCodeGeneration();

        Assert.Equal(
            ["curl", "javascript-fetch", "python-requests", "csharp-httpclient", "powershell", "go-nethttp"],
            dialog.Targets.Select(t => t.Id));
    }
}
