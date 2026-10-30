using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorViewModelTests
{
    private static HttpRequestDefinition Sample() => new(
        Guid.NewGuid(),
        "Get users",
        HttpMethodKind.Post,
        "https://x.test/users",
        [new KeyValueEntry("page", "1")],
        [new KeyValueEntry("Accept", "*/*", false)],
        RequestBody.Raw(RawContentType.Json, "{}"));

    public static TheoryData<string, Action<RequestEditorViewModel>, Action<RequestEditorViewModel>> Edits => new()
    {
        { "name", e => e.Name = "Other", e => e.Name = "Get users" },
        { "url", e => e.Url = "https://y.test", e => e.Url = "https://x.test/users" },
        { "method", e => e.Method = HttpMethodKind.Get, e => e.Method = HttpMethodKind.Post },
        { "header", e => e.Headers.Rows[0].Value = "text/plain", e => e.Headers.Rows[0].Value = "*/*" },
        { "param", e => e.QueryParams.Rows[0].Key = "p", e => e.QueryParams.Rows[0].Key = "page" },
        { "body kind", e => e.BodyKind = BodyKind.None, e => e.BodyKind = BodyKind.Raw },
        { "body text", e => e.BodyText = "[]", e => e.BodyText = "{}" },
    };

    [Fact]
    public void RoundTripIsEquivalent()
    {
        var definition = Sample();

        Assert.True(RequestEditorViewModel.FromDefinition(definition).ToDefinition().IsEquivalentTo(definition));
    }

    [Fact]
    public void LoadedRequestIsClean()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample());

        Assert.False(editor.IsDirty);
        Assert.Equal("Get users", editor.Title);
    }

    [Theory]
    [MemberData(nameof(Edits))]
    public void EditMakesDirtyAndRevertMakesClean(string field, Action<RequestEditorViewModel> edit, Action<RequestEditorViewModel> revert)
    {
        Assert.NotEmpty(field);
        var editor = RequestEditorViewModel.FromDefinition(Sample());
        var cleanTitle = editor.Title;
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        edit(editor);

        Assert.True(editor.IsDirty);
        Assert.EndsWith(" •", editor.Title);
        Assert.Contains(nameof(RequestEditorViewModel.IsDirty), raised);
        Assert.Contains(nameof(RequestEditorViewModel.Title), raised);

        revert(editor);

        Assert.False(editor.IsDirty);
        Assert.Equal(cleanTitle, editor.Title);
    }

    [Fact]
    public void MarkSavedClearsDirty()
    {
        var editor = RequestEditorViewModel.CreateNew();
        editor.Url = "https://x.test";

        editor.MarkSaved();

        Assert.False(editor.IsNew);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void CreateNewIsNewAndDirty()
    {
        var editor = RequestEditorViewModel.CreateNew();

        Assert.True(editor.IsNew);
        Assert.True(editor.IsDirty);
        Assert.Equal("New Request •", editor.Title);
    }

    [Fact]
    public void BlankNameBecomesDefault()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample());
        editor.Name = "   ";

        Assert.Equal("New Request", editor.ToDefinition().Name);
        Assert.StartsWith("New Request", editor.Title);
    }
}
