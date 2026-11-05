using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FolderYamlSerializerTests
{
    private const string IdText = "6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8";

    private static FolderDefinition Sample() => new(
        Guid.Parse(IdText),
        "Users – café ☕",
        "First line\nSecond line: with colon",
        [new("baseUrl", "https://api.example.com"), new("token", "{{secret}}", false)]);

    private static string Golden() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Storage", "golden", "folder-v1.folder.yaml"));

    [Fact]
    public void RoundTripKeepsEquivalence()
    {
        var folder = Sample();
        Assert.True(folder.IsEquivalentTo(FolderYamlSerializer.Deserialize(FolderYamlSerializer.Serialize(folder))));
    }

    [Fact]
    public void RoundTripKeepsEmptyFolder()
    {
        var folder = FolderDefinition.Create("Empty");
        Assert.True(folder.IsEquivalentTo(FolderYamlSerializer.Deserialize(FolderYamlSerializer.Serialize(folder))));
    }

    [Fact]
    public void SerializeMatchesGoldenFile() => Assert.Equal(Golden(), FolderYamlSerializer.Serialize(Sample()));

    [Fact]
    public void GoldenFileDeserializes() =>
        Assert.True(Sample().IsEquivalentTo(FolderYamlSerializer.Deserialize(Golden())));

    [Fact]
    public void MissingOptionalKeysTakeDefaults()
    {
        var folder = FolderYamlSerializer.Deserialize($"version: 1\nid: {IdText}\n");
        Assert.Equal("Untitled", folder.Name);
        Assert.Equal("", folder.Description);
        Assert.Empty(folder.Variables);
    }

    [Fact]
    public void UnknownKeyIsIgnored()
    {
        var folder = FolderYamlSerializer.Deserialize($"version: 1\nid: {IdText}\nname: A\nfuture: 1\n");
        Assert.Equal("A", folder.Name);
    }

    [Theory]
    [InlineData("version: 2\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\n")]
    [InlineData("name: [unclosed\n")]
    [InlineData("version: 1\nname: X\n")]
    [InlineData("version: 1\nid: nope\n")]
    public void InvalidFilesThrowStorageException(string yaml) =>
        Assert.Throws<StorageException>(() => FolderYamlSerializer.Deserialize(yaml));

    [Fact]
    public void ScriptsRoundTripForJavaScriptAndLua()
    {
        var scripts = new ScriptSet(
            new ScriptBlock(ScriptLanguage.JavaScript, "pm.test(\"a: b\", () => {});\n  indented\n"),
            new ScriptBlock(ScriptLanguage.Lua, " leading space\nprint('é ☕')"));
        var folder = Sample() with { Scripts = scripts };
        Assert.Equal(scripts, FolderYamlSerializer.Deserialize(FolderYamlSerializer.Serialize(folder)).Scripts);
    }

    [Fact]
    public void ScriptsAreWrittenLastWithBlockKeyOrder()
    {
        var folder = FolderDefinition.Create("F") with
        {
            Scripts = new ScriptSet(new ScriptBlock(ScriptLanguage.Lua, "a()\nb()\n"), ScriptBlock.Empty),
        };
        var yaml = FolderYamlSerializer.Serialize(folder);
        Assert.EndsWith(
            "scripts:\n  preRequest:\n    language: lua\n    source: |\n      a()\n      b()\n"
            + "  postResponse:\n    language: javascript\n    source: \"\"\n",
            yaml);
        Assert.True(yaml.IndexOf("variables:", StringComparison.Ordinal) < yaml.IndexOf("scripts:", StringComparison.Ordinal));
    }

    [Fact]
    public void MilestoneTwoFolderLoadsWithEmptyScripts() =>
        Assert.Equal(
            ScriptSet.Empty,
            FolderYamlSerializer.Deserialize($"version: 1\nid: {IdText}\nname: A\ndescription: \"\"\nvariables: []\n").Scripts);

    [Fact]
    public void UnknownScriptLanguageThrows() =>
        Assert.Throws<StorageException>(() => FolderYamlSerializer.Deserialize(
            $"version: 1\nid: {IdText}\nscripts:\n  preRequest:\n    language: python\n    source: x\n"));

    [Fact]
    public void Version2MessageNamesTheVersion()
    {
        var ex = Assert.Throws<StorageException>(() =>
            FolderYamlSerializer.Deserialize($"version: 2\nid: {IdText}\n"));
        Assert.Equal("Unsupported folder file version: 2", ex.Message);
    }
}
