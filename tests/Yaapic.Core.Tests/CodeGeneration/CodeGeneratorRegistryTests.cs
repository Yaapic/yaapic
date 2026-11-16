using Yaapic.Core.CodeGeneration;

namespace Yaapic.Core.Tests.CodeGeneration;

public class CodeGeneratorRegistryTests
{
    private sealed class Fake(string id) : ICodeGenerator
    {
        public string Id => id;

        public string DisplayName => id;

        public string EditorLanguage => "plaintext";

        public string Generate(CodeRequest request) => id;
    }

    [Fact]
    public void All_keeps_registration_order_and_Find_looks_up()
    {
        var a = new Fake("a");
        var b = new Fake("b");
        var reg = new CodeGeneratorRegistry([b, a]);
        Assert.Equal([b, a], reg.All);
        Assert.Same(a, reg.Find("a"));
        Assert.Null(reg.Find("zzz"));
    }

    [Fact]
    public void Duplicate_ids_are_rejected() =>
        Assert.Throws<ArgumentException>(() => new CodeGeneratorRegistry([new Fake("a"), new Fake("a")]));
}
