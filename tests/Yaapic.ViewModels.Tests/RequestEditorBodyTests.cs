using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorBodyTests
{
    private static RequestSender Sender() =>
        new(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "unused"))));

    private static HttpRequestDefinition With(RequestBody body) =>
        HttpRequestDefinition.Create("r") with { Method = HttpMethodKind.Post, Url = "https://x.test", Body = body };

    private static RequestEditorViewModel Editor(RequestBody body, FakeDialogService? dialogs = null) =>
        RequestEditorViewModel.FromDefinition(With(body), Sender(), new FakeVariableScopeProvider(), dialogs: dialogs);

    private static readonly FormField[] Fields =
    [
        new("a", "1"),
        new("f", "/tmp/x.bin", false, FormFieldKind.File),
    ];

    public static TheoryData<RequestBody> Bodies => new()
    {
        RequestBody.None,
        RequestBody.Raw(RawContentType.Json, "{}"),
        new RequestBody(BodyKind.UrlEncoded, RawContentType.Text, "", [new FormField("a", "1"), new FormField("b", "2", false)], ""),
        new RequestBody(BodyKind.FormData, RawContentType.Text, "", Fields, ""),
        new RequestBody(BodyKind.Binary, RawContentType.Text, "", [], "/tmp/y.bin"),
    };

    [Fact]
    public void OffersAllBodyKinds() =>
        Assert.Equal([BodyKind.None, BodyKind.Raw, BodyKind.UrlEncoded, BodyKind.FormData, BodyKind.Binary], RequestEditorViewModel.BodyKinds);

    [Theory]
    [MemberData(nameof(Bodies))]
    public void EachBodyKindRoundTrips(RequestBody body)
    {
        var editor = Editor(body);

        Assert.Equal(body, editor.ToDefinition().Body);
        Assert.False(editor.IsDirty);
    }

    public static TheoryData<string, Action<RequestEditorViewModel>, Action<RequestEditorViewModel>> Edits => new()
    {
        { "form value", e => e.FormData.Rows[0].Value = "2", e => e.FormData.Rows[0].Value = "1" },
        { "form to file", e => e.FormData.Rows[0].Kind = FormFieldKind.File, e => e.FormData.Rows[0].Kind = FormFieldKind.Text },
        { "form enabled", e => e.FormData.Rows[1].Enabled = true, e => e.FormData.Rows[1].Enabled = false },
        { "form remove", e => e.FormData.RemoveRowCommand.Execute(e.FormData.Rows[0]), e => e.FormData.Load(Fields) },
        { "form add", e => e.FormData.Rows[^1].Key = "n", e => e.FormData.Rows[^2].Key = "" },
    };

    [Theory]
    [MemberData(nameof(Edits))]
    public void FormEditsMakeTabDirtyAndRevertingCleansIt(string name, Action<RequestEditorViewModel> edit, Action<RequestEditorViewModel> revert)
    {
        var editor = Editor(new RequestBody(BodyKind.FormData, RawContentType.Text, "", Fields, ""));

        edit(editor);
        Assert.True(editor.IsDirty, name);

        revert(editor);
        if (name == "form remove")
        {
            // Load does not raise Changed: refresh through a no-op edit.
            editor.Name = "r2";
            editor.Name = "r";
        }

        Assert.False(editor.IsDirty, name);
    }

    [Fact]
    public void UrlEncodedEditAndRevert()
    {
        var editor = Editor(new RequestBody(BodyKind.UrlEncoded, RawContentType.Text, "", [new FormField("a", "1")], ""));

        editor.UrlEncodedFields.Rows[0].Value = "2";
        Assert.True(editor.IsDirty);

        editor.UrlEncodedFields.Rows[0].Value = "1";
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void BinaryPathEditAndRevert()
    {
        var editor = Editor(new RequestBody(BodyKind.Binary, RawContentType.Text, "", [], "/a"));

        editor.BinaryFilePath = "/b";
        Assert.True(editor.IsDirty);

        editor.BinaryFilePath = "/a";
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void PlaceholderRowNeverAppearsInFields()
    {
        var editor = new FormDataEditorViewModel();
        editor.Load(Fields);

        Assert.Equal(3, editor.Rows.Count);
        Assert.Equal(Fields, editor.ToFields());
        Assert.Equal(1, editor.ActiveCount);
    }

    [Fact]
    public void PlaceholderRowCannotBeRemoved()
    {
        var editor = new FormDataEditorViewModel();

        editor.RemoveRowCommand.Execute(editor.Rows[0]);

        Assert.Single(editor.Rows);
    }

    [Fact]
    public void OnlyActiveKindFieldsAreWritten()
    {
        var editor = Editor(new RequestBody(BodyKind.FormData, RawContentType.Text, "", Fields, ""));
        editor.UrlEncodedFields.Rows[0].Key = "u";
        editor.BinaryFilePath = "/z";

        Assert.Equal(Fields, editor.ToDefinition().Body.Fields);
        Assert.Equal("", editor.ToDefinition().Body.FilePath);

        editor.BodyKind = BodyKind.UrlEncoded;
        Assert.Equal([new FormField("u", "")], editor.ToDefinition().Body.Fields);

        editor.BodyKind = BodyKind.Binary;
        Assert.Empty(editor.ToDefinition().Body.Fields);
        Assert.Equal("/z", editor.ToDefinition().Body.FilePath);

        editor.BodyKind = BodyKind.Raw;
        Assert.Empty(editor.ToDefinition().Body.Fields);
        Assert.Equal("", editor.ToDefinition().Body.FilePath);
    }

    [Fact]
    public async Task PickFileSetsValueAndKeepsItOnCancel()
    {
        var dialogs = new FakeDialogService { PickFileAnswer = "/p/q.txt" };
        var editor = Editor(new RequestBody(BodyKind.FormData, RawContentType.Text, "", Fields, ""), dialogs);

        await editor.FormData.Rows[0].PickFileAsync();
        Assert.Equal("/p/q.txt", editor.FormData.Rows[0].Value);
        Assert.True(editor.IsDirty);

        dialogs.PickFileAnswer = null;
        await editor.FormData.Rows[0].PickFileAsync();
        Assert.Equal("/p/q.txt", editor.FormData.Rows[0].Value);
    }

    [Fact]
    public async Task PickBinaryFileSetsPathAndKeepsItOnCancel()
    {
        var dialogs = new FakeDialogService { PickFileAnswer = "/n.bin" };
        var editor = Editor(new RequestBody(BodyKind.Binary, RawContentType.Text, "", [], "/o.bin"), dialogs);

        await editor.PickBinaryFileCommand.ExecuteAsync(null);
        Assert.Equal("/n.bin", editor.BinaryFilePath);

        dialogs.PickFileAnswer = null;
        await editor.PickBinaryFileCommand.ExecuteAsync(null);
        Assert.Equal("/n.bin", editor.BinaryFilePath);
    }

    [Fact]
    public async Task PickCommandsDoNothingWithoutDialogService()
    {
        var editor = Editor(new RequestBody(BodyKind.Binary, RawContentType.Text, "", [], "/o.bin"));

        await editor.PickBinaryFileCommand.ExecuteAsync(null);
        await editor.FormData.Rows[0].PickFileAsync();

        Assert.Equal("/o.bin", editor.BinaryFilePath);
        Assert.Equal("", editor.FormData.Rows[0].Value);
    }
}
