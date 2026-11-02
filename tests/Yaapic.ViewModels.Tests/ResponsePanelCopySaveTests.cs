using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Responses;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class ResponsePanelCopySaveTests
{
    private static SendReport Report(byte[] body, string? contentType) =>
        new(
            SendResult.Success(new HttpResponseData(
                200,
                "OK",
                contentType is null ? [] : [new KeyValueEntry("Content-Type", contentType)],
                body,
                TimeSpan.FromMilliseconds(1),
                "https://x.test")),
            []);

    [Fact]
    public async Task Copy_sends_the_displayed_text_in_pretty_and_raw_modes()
    {
        var fake = new FakeDialogService();
        var vm = new ResponsePanelViewModel(fake);
        vm.ShowReport(Report(Encoding.UTF8.GetBytes("{\"a\":1}"), "application/json"));

        await vm.CopyBodyCommand.ExecuteAsync(null);
        vm.ViewMode = BodyViewMode.Raw;
        await vm.CopyBodyCommand.ExecuteAsync(null);

        Assert.Equal(2, fake.ClipboardTexts.Count);
        Assert.Contains('\n', fake.ClipboardTexts[0]);
        Assert.Equal("{\"a\":1}", fake.ClipboardTexts[1]);
    }

    [Fact]
    public void Copy_is_disabled_without_response_and_for_binary_but_save_is_enabled_for_binary()
    {
        var vm = new ResponsePanelViewModel(new FakeDialogService());
        Assert.False(vm.CopyBodyCommand.CanExecute(null));
        Assert.False(vm.SaveBodyCommand.CanExecute(null));

        vm.ShowReport(Report([0, 1, 2, 0xFF], "application/octet-stream"));

        Assert.True(vm.IsBinary);
        Assert.False(vm.CopyBodyCommand.CanExecute(null));
        Assert.True(vm.SaveBodyCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("application/json; charset=utf-8", "response.json")]
    [InlineData("application/xml", "response.xml")]
    [InlineData("text/html", "response.html")]
    [InlineData("text/javascript", "response.js")]
    [InlineData("text/plain", "response.txt")]
    [InlineData("text/csv", "response.txt")]
    [InlineData("image/png", "response.bin")]
    [InlineData(null, "response.bin")]
    public async Task Save_passes_the_raw_bytes_and_a_suggested_name(string? contentType, string name)
    {
        var fake = new FakeDialogService();
        var vm = new ResponsePanelViewModel(fake);
        byte[] raw = Encoding.UTF8.GetBytes("{\"a\":1}");
        vm.ShowReport(Report(raw, contentType));

        await vm.SaveBodyCommand.ExecuteAsync(null);

        var saved = Assert.Single(fake.SavedFiles);
        Assert.Equal(name, saved.Name);
        Assert.Equal(raw, saved.Content);
    }

    [Fact]
    public void Commands_are_disabled_after_clear_and_raise_can_execute_changed()
    {
        var vm = new ResponsePanelViewModel(new FakeDialogService());
        var raised = 0;
        vm.CopyBodyCommand.CanExecuteChanged += (_, _) => raised++;

        vm.ShowReport(Report("hi"u8.ToArray(), "text/plain"));
        Assert.True(vm.CopyBodyCommand.CanExecute(null));
        vm.Clear();

        Assert.False(vm.CopyBodyCommand.CanExecute(null));
        Assert.False(vm.SaveBodyCommand.CanExecute(null));
        Assert.True(raised > 0);
    }

    [Fact]
    public async Task Null_dialog_service_does_nothing()
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report("hi"u8.ToArray(), "text/plain"));

        await vm.CopyBodyCommand.ExecuteAsync(null);
        await vm.SaveBodyCommand.ExecuteAsync(null);
    }
}
