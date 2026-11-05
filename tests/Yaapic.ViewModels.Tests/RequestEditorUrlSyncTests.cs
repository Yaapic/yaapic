using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorUrlSyncTests
{
    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");

    private static RequestEditorViewModel Editor(FakeHttpTransport? transport = null) =>
        RequestEditorViewModel.CreateNew(new RequestSender(transport ?? new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok)))), new FakeVariableScopeProvider());

    private static string[] Rows(RequestEditorViewModel e) =>
        [.. e.QueryParams.Rows.Where(r => !r.IsEmpty).Select(r => (r.Enabled ? "" : "!") + r.Key + "=" + r.Value)];

    [Fact]
    public void TypingTheUrlFillsTheTable()
    {
        var editor = Editor();

        editor.Url = "https://x.test/a?b=1&c=2";

        Assert.Equal(["b=1", "c=2"], Rows(editor));
        Assert.Equal("https://x.test/a?b=1&c=2", editor.Url);
    }

    [Fact]
    public void RemovingAPairFromTheUrlRemovesTheRow()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1&c=2";

        editor.Url = "https://x.test/a?b=1";

        Assert.Equal(["b=1"], Rows(editor));
    }

    [Fact]
    public void EditingARowUpdatesTheUrl()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1";

        editor.QueryParams.Rows[0].Value = "9";

        Assert.Equal("https://x.test/a?b=9", editor.Url);
    }

    [Fact]
    public void UncheckingARowDropsItFromTheUrlAndRecheckingRestoresIt()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1&c=2";

        editor.QueryParams.Rows[0].Enabled = false;

        Assert.Equal("https://x.test/a?c=2", editor.Url);
        Assert.Equal(["!b=1", "c=2"], Rows(editor));

        editor.QueryParams.Rows[0].Enabled = true;

        Assert.Equal("https://x.test/a?b=1&c=2", editor.Url);
    }

    [Fact]
    public void TypingKeepsDisabledRowsInPlace()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1&c=2";
        editor.QueryParams.Rows[0].Enabled = false;

        editor.Url = "https://x.test/a?c=3&d=4";

        Assert.Equal(["!b=1", "c=3", "d=4"], Rows(editor));
    }

    [Fact]
    public void FragmentIsKept()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1#top";

        editor.QueryParams.Rows[0].Value = "2";

        Assert.Equal("https://x.test/a?b=2#top", editor.Url);
    }

    [Fact]
    public void VariablesStayUnescaped()
    {
        var editor = Editor();
        editor.Url = "{{baseUrl}}/a?id={{id}}";

        editor.QueryParams.Rows[0].Value = "{{id}}";

        Assert.Equal("{{baseUrl}}/a?id={{id}}", editor.Url);
    }

    [Fact]
    public void LegacyInlineQueryLoadsCleanWithEntriesFirst()
    {
        var definition = HttpRequestDefinition.Create("r") with
        {
            Url = "https://x.test/a?b=1",
            QueryParams = [new KeyValueEntry("c", "2")],
        };

        var editor = RequestEditorViewModel.FromDefinition(definition, new RequestSender(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok)))), new FakeVariableScopeProvider());

        Assert.Equal(["b=1", "c=2"], Rows(editor));
        Assert.Equal("https://x.test/a?b=1&c=2", editor.Url);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void ToDefinitionUrlHasNoQuery()
    {
        var editor = Editor();
        editor.Url = "https://x.test/a?b=1#top";

        var definition = editor.ToDefinition();

        Assert.Equal("https://x.test/a#top", definition.Url);
        Assert.Equal("b", Assert.Single(definition.QueryParams).Key);
    }

    [Fact]
    public async Task SendsEachPairOnce()
    {
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(Ok)));
        var editor = Editor(transport);
        editor.Url = "https://x.test/a?b=1";
        editor.QueryParams.Rows[^1].Key = "c";
        editor.QueryParams.Rows[^2].Value = "2";

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal("https://x.test/a?b=1&c=2", Assert.Single(transport.Requests).Url);
    }
}
