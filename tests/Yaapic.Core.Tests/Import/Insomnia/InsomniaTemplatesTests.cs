using Yaapic.Core.Import;
using Yaapic.Core.Import.Insomnia;

namespace Yaapic.Core.Tests.Import.Insomnia;

public class InsomniaTemplatesTests
{
    private static string Translate(string text, out ImportReport report)
    {
        report = new();
        return InsomniaTemplates.Translate(text, "r", report);
    }

    [Theory]
    [InlineData("{{ _.name }}", "{{name}}")]
    [InlineData("{{_.name}}", "{{name}}")]
    [InlineData("{{ _['name'] }}", "{{name}}")]
    [InlineData("{{ _[\"name\"] }}", "{{name}}")]
    [InlineData("{{ _.a.b }}", "{{a.b}}")]
    [InlineData("{{ _.baseUrl }}/users", "{{baseUrl}}/users")]
    [InlineData("{% uuid 'v4' %}", "{{$guid}}")]
    [InlineData("{%uuid  'v4'%}", "{{$guid}}")]
    [InlineData("{% now 'iso-8601' %}", "{{$isoTimestamp}}")]
    [InlineData("{% now 'iso-8601', '' %}", "{{$isoTimestamp}}")]
    [InlineData("{% now 'unix' %}", "{{$timestamp}}")]
    [InlineData("{% now 'unix', '' %}", "{{$timestamp}}")]
    [InlineData("plain text", "plain text")]
    public void Converts(string input, string expected)
    {
        Assert.Equal(expected, Translate(input, out var report));
        Assert.Empty(report.Entries);
    }

    [Theory]
    [InlineData("{% response 'body', 'req_1', 'b64::JC5pZA==::46b', 'never', 60 %}", "response")]
    [InlineData("{% base64 'encode', 'x' %}", "base64")]
    [InlineData("{% uuid 'v1' %}", "uuid")]
    [InlineData("{% now 'millis' %}", "now")]
    public void Unsupported_tag_is_kept_and_warned_once(string tag, string name)
    {
        var text = tag + " and " + tag;
        Assert.Equal(text, Translate(text, out var report));
        var warning = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
        Assert.Contains(name, warning.Message);
    }

    [Fact]
    public void Non_underscore_expression_is_kept_and_warned()
    {
        Assert.Equal("{{ response 'x' }}", Translate("{{ response 'x' }}", out var report));
        Assert.Single(report.Entries);
    }
}
