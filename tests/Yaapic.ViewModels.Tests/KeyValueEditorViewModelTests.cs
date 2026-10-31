using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Tests;

public sealed class KeyValueEditorViewModelTests
{
    [Fact]
    public void NewEditorHasOnePlaceholder()
    {
        var editor = new KeyValueEditorViewModel();

        var row = Assert.Single(editor.Rows);
        Assert.True(row.IsEmpty);
    }

    [Fact]
    public void TypingAKeyAppendsAPlaceholder()
    {
        var editor = new KeyValueEditorViewModel();

        editor.Rows[0].Key = "a";

        Assert.Equal(2, editor.Rows.Count);
        Assert.True(editor.Rows[1].IsEmpty);
    }

    [Fact]
    public void RemovingThePlaceholderIsANoOp()
    {
        var editor = new KeyValueEditorViewModel();
        var changed = 0;
        editor.Changed += (_, _) => changed++;

        editor.RemoveRowCommand.Execute(editor.Rows[0]);

        Assert.Single(editor.Rows);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void LoadThenToEntriesKeepsOrderAndEnabled()
    {
        var editor = new KeyValueEditorViewModel();
        KeyValueEntry[] entries = [new("b", "1", false), new("a", "2")];

        editor.Load(entries);

        Assert.Equal(3, editor.Rows.Count);
        Assert.Equal(entries, editor.ToEntries());
    }

    [Fact]
    public void ToEntriesSkipsEmptyRows()
    {
        var editor = new KeyValueEditorViewModel();
        editor.Load([new("a", "1"), new("", ""), new("b", "2")]);

        Assert.Equal(["a", "b"], editor.ToEntries().Select(e => e.Key));
    }

    [Fact]
    public void ChangedIsRaisedOnEditToggleAndRemoveButNotOnLoad()
    {
        var editor = new KeyValueEditorViewModel();
        var changed = 0;
        editor.Changed += (_, _) => changed++;

        editor.Load([new("a", "1")]);
        Assert.Equal(0, changed);

        editor.Rows[0].Value = "2";
        Assert.Equal(1, changed);

        editor.Rows[0].Enabled = false;
        Assert.Equal(2, changed);

        var removed = editor.Rows[0];
        editor.RemoveRowCommand.Execute(removed);
        Assert.Equal(3, changed);

        removed.Key = "x";
        Assert.Equal(3, changed);
    }

    [Fact]
    public void ActiveCountTracksTypingTogglingAndRemoving()
    {
        var editor = new KeyValueEditorViewModel();
        var raised = 0;
        editor.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(KeyValueEditorViewModel.ActiveCount) ? 1 : 0;
        Assert.Equal(0, editor.ActiveCount);

        editor.Rows[0].Key = "a";
        Assert.Equal(1, editor.ActiveCount);
        Assert.True(raised > 0);

        editor.Rows[0].Key = "";
        editor.Rows[0].Key = "b";
        editor.Rows[1].Key = "c";
        Assert.Equal(2, editor.ActiveCount);

        editor.Rows[0].Enabled = false;
        Assert.Equal(1, editor.ActiveCount);

        editor.RemoveRowCommand.Execute(editor.Rows[1]);
        Assert.Equal(0, editor.ActiveCount);

        editor.Load([new("a", "1"), new("b", "2", false)]);
        Assert.Equal(1, editor.ActiveCount);
    }
}
