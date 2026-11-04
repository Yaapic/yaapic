using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class HistoryViewModelTests
{
    // UTC+2, no daylight saving.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");

    private readonly InMemoryHistoryStore _store = new();
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private static SendReport Report(SendResult result) => new(result, []);

    private static HttpRequestDefinition Request(string name, string url = "https://x.test", HttpMethodKind method = HttpMethodKind.Get) =>
        HttpRequestDefinition.Create(name) with { Url = url, Method = method };

    private HistoryViewModel Create() => new(_store, _time);

    [Fact]
    public async Task RecordsSuccessAndFailureNewestFirst()
    {
        var history = Create();
        Assert.True(history.IsEmpty);

        await history.RecordAsync(Request("a"), Report(SendResult.Success(Ok)));
        _time.Now = _time.Now.AddMinutes(5);
        await history.RecordAsync(Request("b"), Report(SendResult.Fail(SendFailureKind.Timeout, "timed out")));

        Assert.False(history.IsEmpty);
        var group = Assert.Single(history.Groups);
        Assert.Equal("Today", group.Title);
        Assert.Equal(["b", "a"], group.Items.Select(i => i.Name));
        Assert.True(group.Items[0].IsFailed);
        Assert.Equal("timed out", group.Items[0].StatusText);
        Assert.False(group.Items[1].IsFailed);
        Assert.Equal("200 OK", group.Items[1].StatusText);
        Assert.Equal(StatusCategory.Success, group.Items[1].StatusCategory);
        Assert.Equal("5 ms", group.Items[1].DurationText);
        Assert.Equal("14:00", group.Items[1].TimeText);
        Assert.Equal(2, (await _store.ListAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task CanceledSendsAreNotRecorded()
    {
        var history = Create();

        await history.RecordAsync(Request("a"), Report(SendResult.Fail(SendFailureKind.Canceled, "Request canceled")));

        Assert.True(history.IsEmpty);
        Assert.Empty(await _store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GroupsByLocalDay()
    {
        // 23:30 UTC on 11-03 is already 11-04 locally (UTC+2); 21:00 UTC on 11-02 is 11-02 23:00 locally.
        var history = Create();
        _time.Now = new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.Zero);
        await history.RecordAsync(Request("old"), Report(SendResult.Success(Ok)));
        _time.Now = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        await history.RecordAsync(Request("yesterday"), Report(SendResult.Success(Ok)));
        _time.Now = new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero);
        await history.RecordAsync(Request("today"), Report(SendResult.Success(Ok)));
        _time.Now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        await history.RefreshAsync();

        Assert.Equal(["Today", "Yesterday", "2026-11-02"], history.Groups.Select(g => g.Title));
        Assert.Equal(["today"], history.Groups[0].Items.Select(i => i.Name));
        Assert.Equal(["yesterday"], history.Groups[1].Items.Select(i => i.Name));
        Assert.Equal(["old"], history.Groups[2].Items.Select(i => i.Name));
        Assert.Equal("23:00", history.Groups[2].Items[0].TimeText);
    }

    [Theory]
    [InlineData("post", "create")]
    [InlineData("CREATE", "create")]
    [InlineData("/users", "create")]
    [InlineData("404", "missing")]
    [InlineData("  not found ", "missing")]
    public async Task FilterMatchesMethodNameUrlAndStatus(string filter, string expected)
    {
        var history = Create();
        await history.RecordAsync(Request("create", "https://x.test/users", HttpMethodKind.Post), Report(SendResult.Success(Ok)));
        _time.Now = _time.Now.AddMinutes(1);
        await history.RecordAsync(
            Request("missing", "https://x.test/other"),
            Report(SendResult.Success(Ok with { StatusCode = 404, ReasonPhrase = "Not Found" })));

        history.FilterText = filter;

        Assert.Equal([expected], history.Groups.SelectMany(g => g.Items).Select(i => i.Name));

        history.FilterText = "";

        Assert.Equal(2, history.Groups.SelectMany(g => g.Items).Count());
    }

    [Fact]
    public async Task FilterWithNoMatchIsNotEmpty()
    {
        var history = Create();
        await history.RecordAsync(Request("a"), Report(SendResult.Success(Ok)));

        history.FilterText = "zzz";

        Assert.Empty(history.Groups);
        Assert.False(history.IsEmpty);
    }

    [Fact]
    public async Task FailingStoreSetsLastErrorWithoutThrowing()
    {
        var history = Create();
        _store.FailureMessage = "disk full";

        await history.RecordAsync(Request("a"), Report(SendResult.Success(Ok)));

        Assert.Equal("disk full", history.LastError);
        Assert.True(history.IsEmpty);

        await history.RefreshAsync();

        Assert.Equal("disk full", history.LastError);

        _store.FailureMessage = null;
        await history.RefreshAsync();

        Assert.Null(history.LastError);
    }

    [Fact]
    public async Task EditorRaisesSentWithRequestAsSent()
    {
        var editor = RequestEditorViewModel.CreateNew(
            new RequestSender(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok)))),
            new FakeVariableScopeProvider());
        editor.Url = "https://x.test/{{id}}";
        RequestSentEventArgs? args = null;
        editor.Sent += (_, e) => args = e;

        await editor.SendCommand.ExecuteAsync(null);

        Assert.NotNull(args);
        Assert.Equal("https://x.test/{{id}}", args.Request.Url);
        Assert.Equal(200, args.Report.Result.Response?.StatusCode);
    }

    [Fact]
    public async Task EditorDoesNotRaiseSentOnCancel()
    {
        var started = new TaskCompletionSource();
        var editor = RequestEditorViewModel.CreateNew(
            new RequestSender(new FakeHttpTransport(async (_, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return SendResult.Success(Ok);
            })),
            new FakeVariableScopeProvider());
        editor.Url = "https://x.test";
        var raised = false;
        editor.Sent += (_, _) => raised = true;

        var send = editor.SendCommand.ExecuteAsync(null);
        await started.Task;
        editor.CancelCommand.Execute(null);
        await send;

        Assert.False(raised);
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => Zone;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
