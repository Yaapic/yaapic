using Yaapic.Core.Scripting;

namespace Yaapic.Core.Tests.Scripting;

public class YaapicToPostmanRewriterTests
{
    [Theory]
    [InlineData("yaapic.environment.set(\"a\", 1)", "pm.environment.set(\"a\", 1)")]
    [InlineData("yaapic.a(); yaapic.b();", "pm.a(); pm.b();")]
    [InlineData("yaapic.test(\"x\", () => {})", "pm.test(\"x\", () => {})")]
    [InlineData("x = 'yaapic.a'", "x = 'yaapic.a'")]
    [InlineData("x = \"say \\\"yaapic.a\\\"\"", "x = \"say \\\"yaapic.a\\\"\"")]
    [InlineData("// yaapic.a\nyaapic.b", "// yaapic.a\npm.b")]
    [InlineData("/* yaapic.a */ yaapic.b", "/* yaapic.a */ pm.b")]
    [InlineData("`a yaapic.b ${yaapic.c}`", "`a yaapic.b ${yaapic.c}`")]
    [InlineData("myyaapic.x", "myyaapic.x")]
    [InlineData("a.yaapic.b", "a.yaapic.b")]
    [InlineData("$yaapic.x _yaapic.x 1yaapic.x", "$yaapic.x _yaapic.x 1yaapic.x")]
    [InlineData("yaapic", "yaapic")]
    [InlineData("let yaapic = 1", "let yaapic = 1")]
    [InlineData("", "")]
    [InlineData("console.log(1)", "console.log(1)")]
    public void Rewrites(string source, string expected) => Assert.Equal(expected, YaapicToPostmanRewriter.Rewrite(source));
}
