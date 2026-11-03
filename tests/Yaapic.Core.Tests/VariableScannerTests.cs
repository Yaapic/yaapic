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
}
