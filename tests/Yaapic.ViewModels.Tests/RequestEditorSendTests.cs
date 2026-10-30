using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorSendTests
{
    private sealed class StubEnvironments(EnvironmentDefinition? current) : IActiveEnvironmentProvider
    {
        public EnvironmentDefinition? Current { get; } = current;
    }

    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");

    private static RequestEditorViewModel Editor(FakeHttpTransport transport, EnvironmentDefinition? environment = null, string url = "https://x.test")
    {
        var editor = RequestEditorViewModel.CreateNew(new RequestSender(transport), new StubEnvironments(environment));
        editor.Url = url;
        return editor;
    }

    [Fact]
    public async Task SendsUnsavedDefinitionWithActiveEnvironment()
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "dev", [new KeyValueEntry("host", "api.test")]);
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok)));
        var editor = Editor(transport, environment, "https://{{host}}/users");

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal("https://api.test/users", Assert.Single(transport.Requests).Url);
        Assert.Equal(ResponseState.Completed, editor.Response.State);
    }

    [Fact]
    public async Task PanelIsSendingWhileInFlight()
    {
        var gate = new TaskCompletionSource<SendResult>();
        var editor = Editor(new FakeHttpTransport((_, _) => gate.Task));

        var send = editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Sending, editor.Response.State);
        Assert.True(editor.IsSending);
        Assert.False(editor.SendCommand.CanExecute(null));

        gate.SetResult(SendResult.Success(Ok));
        await send;

        Assert.Equal(ResponseState.Completed, editor.Response.State);
        Assert.False(editor.IsSending);
    }

    [Fact]
    public async Task TransportFailureFailsThePanel()
    {
        var editor = Editor(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.ConnectionFailed, "boom"))));

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Failed, editor.Response.State);
        Assert.Equal("boom", editor.Response.ErrorMessage);
    }

    [Fact]
    public async Task CancelShowsCanceledMessage()
    {
        var started = new TaskCompletionSource();
        var editor = Editor(new FakeHttpTransport(async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return SendResult.Success(Ok);
        }));

        var send = editor.SendCommand.ExecuteAsync(null);
        await started.Task;
        editor.CancelCommand.Execute(null);
        await send;

        Assert.Equal(ResponseState.Failed, editor.Response.State);
        Assert.Equal("Request canceled", editor.Response.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CannotSendWithBlankUrl(string url)
    {
        var editor = Editor(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok))), url: url);

        Assert.False(editor.SendCommand.CanExecute(null));

        editor.Url = "https://x.test";

        Assert.True(editor.SendCommand.CanExecute(null));
    }
}
