using System.Text;
using Yaapic.Core.Diff;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class ResponseDifferTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.UnixEpoch;

    private static ResponseSnapshot Snap(int code = 200, string body = "a\nb", params (string, string)[] headers) =>
        new(code, "OK", [.. headers.Select(h => new KeyValueEntry(h.Item1, h.Item2))], body, null, T);

    private static ResponseDiff Diff(ResponseSnapshot a, ResponseSnapshot b, ResponseDiffOptions? o = null) =>
        ResponseDiffer.Compare(a, b, o ?? new ResponseDiffOptions());

    private static HttpResponseData Response(byte[] body, string? contentType) => new(
        200, "OK", contentType is null ? [] : [new KeyValueEntry("Content-Type", contentType)], body, TimeSpan.Zero, "http://x");

    [Fact]
    public void Snapshot_is_null_for_binary_body()
    {
        Assert.Null(ResponseSnapshot.TryCreate(Response([1, 0, 2], "application/octet-stream"), T));
    }

    [Fact]
    public void Snapshot_is_null_above_one_mebibyte()
    {
        var body = new byte[ResponseSnapshot.MaxBodyBytes + 1];
        Array.Fill(body, (byte)'a');
        Assert.Null(ResponseSnapshot.TryCreate(Response(body, "text/plain"), T));
    }

    [Fact]
    public void Snapshot_uses_formatted_json()
    {
        var s = ResponseSnapshot.TryCreate(Response(Encoding.UTF8.GetBytes("{\"a\":1}"), "application/json"), T);
        Assert.Equal("{\n  \"a\": 1\n}", s!.BodyText);
        Assert.Equal("application/json", s.ContentType);
    }

    [Fact]
    public void Identical_responses_have_no_changes()
    {
        var a = Snap(200, "a\nb", ("X", "1"));
        var d = Diff(a, a);
        Assert.False(d.StatusChanged);
        Assert.Empty(d.Headers);
        Assert.False(d.BodyStats.HasChanges);
    }

    [Fact]
    public void Status_code_change_is_detected_and_reason_alone_is_not()
    {
        var d = Diff(Snap(200), Snap(404) with { ReasonPhrase = "Not Found" });
        Assert.True(d.StatusChanged);
        Assert.Equal("200 OK", d.OldStatus);
        Assert.Equal("404 Not Found", d.NewStatus);
        Assert.False(Diff(Snap(200), Snap(200) with { ReasonPhrase = "Fine" }).StatusChanged);
    }

    [Fact]
    public void Header_kinds_are_reported_sorted()
    {
        var d = Diff(
            Snap(headers: [("Zeta", "1"), ("Keep", "k"), ("Mid", "old")]),
            Snap(headers: [("Alpha", "n"), ("Keep", "k"), ("Mid", "new")]));
        Assert.Equal(
            [("Alpha", HeaderDiffKind.Added, null, "n"), ("Mid", HeaderDiffKind.Changed, "old", "new"), ("Zeta", HeaderDiffKind.Removed, "1", null)],
            d.Headers.Select(h => (h.Name, h.Kind, h.OldValue, h.NewValue)));
    }

    [Fact]
    public void Header_names_are_case_insensitive()
    {
        Assert.Empty(Diff(Snap(headers: ("etag", "1")), Snap(headers: ("ETag", "1"))).Headers);
    }

    [Fact]
    public void Volatile_headers_are_skipped_by_default_and_listed_on_request()
    {
        var a = Snap(headers: ("Date", "1"));
        var b = Snap(headers: ("Date", "2"));
        Assert.Empty(Diff(a, b).Headers);
        Assert.Single(Diff(a, b, new ResponseDiffOptions(IgnoreVolatileHeaders: false)).Headers);
    }

    [Fact]
    public void Duplicate_header_values_are_joined_in_order()
    {
        var d = Diff(Snap(headers: [("Set-Cookie", "a"), ("Set-Cookie", "b")]), Snap(headers: [("Set-Cookie", "a"), ("Set-Cookie", "c")]));
        var h = Assert.Single(d.Headers);
        Assert.Equal("a, b", h.OldValue);
        Assert.Equal("a, c", h.NewValue);
    }

    [Fact]
    public void Body_line_changes_have_stats()
    {
        var d = Diff(Snap(body: "a\nb\nc"), Snap(body: "a\nx\nc\nd"));
        Assert.Equal(new DiffStats(2, 1, true), d.BodyStats);
    }

    [Fact]
    public void Ignore_whitespace_option_applies_to_the_body()
    {
        var a = Snap(body: "a\nb");
        var b = Snap(body: "  a\nb  ");
        Assert.True(Diff(a, b).BodyStats.HasChanges);
        Assert.False(Diff(a, b, new ResponseDiffOptions(IgnoreWhitespace: true)).BodyStats.HasChanges);
    }

    [Fact]
    public void Option_defaults()
    {
        var o = new ResponseDiffOptions();
        Assert.False(o.IgnoreWhitespace);
        Assert.True(o.IgnoreVolatileHeaders);
    }
}
