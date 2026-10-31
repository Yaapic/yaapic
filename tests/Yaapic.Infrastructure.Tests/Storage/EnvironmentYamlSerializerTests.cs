using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class EnvironmentYamlSerializerTests
{
    private const string IdText = "6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8";

    private static EnvironmentDefinition Sample() => new(
        Guid.Parse(IdText),
        "Staging – café ☕",
        [new("baseUrl", "https://staging.example.com"), new("token", "{{secret}}", false)]);

    private static string Golden() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Storage", "golden", "env-v1.env.yaml"));

    [Fact]
    public void RoundTripKeepsEquivalence()
    {
        var environment = Sample();
        Assert.True(environment.IsEquivalentTo(
            EnvironmentYamlSerializer.Deserialize(EnvironmentYamlSerializer.Serialize(environment))));
    }

    [Fact]
    public void RoundTripKeepsEmptyEnvironment()
    {
        var environment = EnvironmentDefinition.Create("Empty");
        Assert.True(environment.IsEquivalentTo(
            EnvironmentYamlSerializer.Deserialize(EnvironmentYamlSerializer.Serialize(environment))));
    }

    [Fact]
    public void SerializeMatchesGoldenFile() => Assert.Equal(Golden(), EnvironmentYamlSerializer.Serialize(Sample()));

    [Fact]
    public void GoldenFileDeserializes() =>
        Assert.True(Sample().IsEquivalentTo(EnvironmentYamlSerializer.Deserialize(Golden())));

    [Fact]
    public void MissingOptionalKeysTakeDefaults()
    {
        var environment = EnvironmentYamlSerializer.Deserialize(
            $"version: 1\nid: {IdText}\nvariables:\n  - key: a\n    value: b\n");
        Assert.Equal("Untitled", environment.Name);
        Assert.Equal(new KeyValueEntry("a", "b", true), Assert.Single(environment.Variables));
    }

    [Theory]
    [InlineData("version: 2\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\n")]
    [InlineData("name: [unclosed\n")]
    [InlineData("version: 1\nname: X\n")]
    [InlineData("version: 1\nid: nope\n")]
    public void InvalidFilesThrowStorageException(string yaml) =>
        Assert.Throws<StorageException>(() => EnvironmentYamlSerializer.Deserialize(yaml));

    [Fact]
    public void Version2MessageNamesTheVersion()
    {
        var ex = Assert.Throws<StorageException>(() =>
            EnvironmentYamlSerializer.Deserialize($"version: 2\nid: {IdText}\n"));
        Assert.Equal("Unsupported environment file version: 2", ex.Message);
    }
}
