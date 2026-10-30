using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Responses;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class ResponsePanelViewModelTests
{
    private static SendReport Report(
        int status = 200,
        string reason = "OK",
        double ms = 10,
        byte[]? body = null,
        string? contentType = null,
        params string[] unresolved) =>
        new(
            SendResult.Success(new HttpResponseData(
                status,
                reason,
                contentType is null ? [] : [new KeyValueEntry("Content-Type", contentType)],
                body ?? [],
                TimeSpan.FromMilliseconds(ms),
                "https://x.test")),
            unresolved);

    [Theory]
    [InlineData(101, "Switching Protocols", "101 Switching Protocols", StatusCategory.Informational)]
    [InlineData(204, "", "204", StatusCategory.Success)]
    [InlineData(302, "Found", "302 Found", StatusCategory.Redirect)]
    [InlineData(404, "Not Found", "404 Not Found", StatusCategory.ClientError)]
    [InlineData(503, "Service Unavailable", "503 Service Unavailable", StatusCategory.ServerError)]
    public void Status_text_and_category(int code, string reason, string text, StatusCategory category)
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(code, reason));

        Assert.Equal(ResponseState.Completed, vm.State);
        Assert.Equal(text, vm.StatusText);
        Assert.Equal(category, vm.StatusCategory);
    }

    [Theory]
    [InlineData(5, "5 ms")]
    [InlineData(999.6, "1000 ms")]
    [InlineData(1234, "1.23 s")]
    public void Duration_text(double ms, string expected)
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(ms: ms));

        Assert.Equal(expected, vm.DurationText);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(3 * 1024 * 1024, "3.0 MB")]
    public void Size_text(int bytes, string expected)
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(body: new byte[bytes]));

        Assert.Equal(expected, vm.SizeText);
    }

    [Fact]
    public void View_mode_switch_reformats_json_and_is_kept_across_responses()
    {
        var json = Encoding.UTF8.GetBytes("""{"a":1}""");
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(body: json, contentType: "application/json"));
        Assert.Equal("{\n  \"a\": 1\n}", vm.BodyText);
        Assert.Equal("json", vm.BodyLanguage);

        vm.ViewMode = BodyViewMode.Raw;
        Assert.Equal("""{"a":1}""", vm.BodyText);

        vm.ShowReport(Report(body: json, contentType: "application/json"));
        Assert.Equal(BodyViewMode.Raw, vm.ViewMode);
        Assert.Equal("""{"a":1}""", vm.BodyText);
    }

    [Fact]
    public void Binary_body()
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(body: new byte[1536], contentType: "image/png"));

        Assert.True(vm.IsBinary);
        Assert.Equal("", vm.BodyText);
        Assert.Equal("Binary content, 1.5 KB", vm.BinaryText);
    }

    [Fact]
    public void Failure_state_clears_body_and_keeps_warning()
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(body: [65], contentType: "text/plain"));
        vm.ShowReport(new SendReport(SendResult.Fail(SendFailureKind.Timeout, "Timed out"), ["a"]));

        Assert.Equal(ResponseState.Failed, vm.State);
        Assert.Equal("Timed out", vm.ErrorMessage);
        Assert.Equal("", vm.BodyText);
        Assert.Empty(vm.Headers);
        Assert.Equal("Unresolved variables: a", vm.UnresolvedWarning);
    }

    [Fact]
    public void Unresolved_warning_lists_names_and_is_null_otherwise()
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(Report(unresolved: ["a", "b"]));
        Assert.Equal("Unresolved variables: a, b", vm.UnresolvedWarning);
        Assert.Null(vm.ErrorMessage);

        vm.ShowReport(Report());
        Assert.Null(vm.UnresolvedWarning);
    }

    [Fact]
    public void Headers_are_exposed_and_clear_resets_everything()
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowSending();
        Assert.Equal(ResponseState.Sending, vm.State);

        vm.ShowReport(Report(body: [65], contentType: "text/plain", unresolved: "a"));
        Assert.Single(vm.Headers);

        vm.Clear();
        Assert.Equal(ResponseState.Empty, vm.State);
        Assert.Equal("", vm.StatusText);
        Assert.Equal("", vm.BodyText);
        Assert.Empty(vm.Headers);
        Assert.Null(vm.UnresolvedWarning);
    }
}
