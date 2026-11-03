using Yaapic.Core.Variables;

namespace Yaapic.Core.Tests;

public sealed class VariableScannerTests
{
    private sealed class EmptySource : IVariableSource
    {
        public bool TryGet(string name, out string value)
        {
            value = "";
            return false;
        }
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "")]
    [InlineData("{{a}}", "a")]
    [InlineData("{{ a }}", "a")]
    [InlineData("{{}}", "")]
    [InlineData("{{  }}", "")]
    [InlineData("{{a", "")]
    [InlineData("{{{a}}}", "a")]
    [InlineData("{{a}}{{b}}", "a,b")]
    [InlineData("{{b}}/{{a}}/{{b}}", "b,a")]
    public void ScanAgreesWithResolverUnresolvedNames(string? text, string expected)
    {
        var scanned = VariableScanner.Scan(text);

        Assert.Equal(expected, string.Join(",", scanned));
        Assert.Equal(scanned, VariableResolver.Resolve(text, new EmptySource()).Unresolved);
    }

    [Theory]
    [InlineData("{{ a }}", "0:7:a")]
    [InlineData("x{{a}}{{b}}", "1:5:a;6:5:b")]
    [InlineData("{{{a}}}", "1:5:a")]
    [InlineData("{{a", "")]
    [InlineData("{{}}", "")]
    [InlineData("{{a}}-{{a}}", "0:5:a;6:5:a")]
    [InlineData(null, "")]
    public void ScanSpansReportsOffsets(string? text, string expected)
    {
        var spans = VariableScanner.ScanSpans(text);

        Assert.Equal(expected, string.Join(";", spans.Select(s => $"{s.Start}:{s.Length}:{s.Name}")));
        Assert.All(spans, s => Assert.Equal(s.Start + s.Length, s.End));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData(" a ", true)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("a{b", false)]
    [InlineData(null, false)]
    public void IsValidNameFollowsTheTokenRules(string? name, bool expected) =>
        Assert.Equal(expected, VariableScanner.IsValidName(name));
}
