using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Tests;

public sealed class DialogServiceTests
{
    private sealed class DefaultsOnly : IDialogService
    {
        public Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName) => Task.FromResult(UnsavedChoice.Cancel);

        public Task<bool> ConfirmDeleteAsync(string itemName) => Task.FromResult(false);
    }

    [Fact]
    public async Task Default_implementations_do_nothing()
    {
        IDialogService dialogs = new DefaultsOnly();

        Assert.Null(await dialogs.PromptTextAsync("t", "l", "i"));
        Assert.Null(await dialogs.PickFileAsync("t"));
        Assert.False(await dialogs.SaveFileAsync("a.txt", [1]));
        await dialogs.SetClipboardTextAsync("x");
    }

    [Fact]
    public async Task Fake_returns_scripted_prompt_answers_in_order_then_null_and_records_calls()
    {
        var fake = new FakeDialogService();
        fake.PromptAnswers.Enqueue("one");
        fake.PromptAnswers.Enqueue(null);

        Assert.Equal("one", await fake.PromptTextAsync("T1", "L1", "I1"));
        Assert.Null(await fake.PromptTextAsync("T2", "L2", "I2"));
        Assert.Null(await fake.PromptTextAsync("T3", "L3", "I3"));
        Assert.Equal([("T1", "L1", "I1"), ("T2", "L2", "I2"), ("T3", "L3", "I3")], fake.Prompts);
    }

    [Fact]
    public async Task Fake_records_file_and_clipboard_calls()
    {
        var fake = new FakeDialogService { PickFileAnswer = "/a/b.json", SaveFileAnswer = true };

        Assert.Equal("/a/b.json", await fake.PickFileAsync("pick"));
        Assert.True(await fake.SaveFileAsync("out.txt", [1, 2]));
        await fake.SetClipboardTextAsync("copied");

        var saved = Assert.Single(fake.SavedFiles);
        Assert.Equal("out.txt", saved.Name);
        Assert.Equal<byte>([1, 2], saved.Content);
        Assert.Equal(["copied"], fake.ClipboardTexts);
    }
}
