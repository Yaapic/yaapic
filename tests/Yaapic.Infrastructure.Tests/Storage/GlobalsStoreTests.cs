using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class GlobalsStoreTests : IDisposable
{
    private static readonly KeyValueEntry[] Sample =
        [new("baseUrl", "https://example.com"), new("token", "{{secret}}", false)];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_root, "globals.yaml");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static string Golden() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Storage", "golden", "globals-v1.yaml"));

    [Fact]
    public void SerializerRoundTrips() =>
        Assert.Equal(Sample, GlobalsYamlSerializer.Deserialize(GlobalsYamlSerializer.Serialize(Sample)));

    [Fact]
    public void SerializeMatchesGoldenFile() => Assert.Equal(Golden(), GlobalsYamlSerializer.Serialize(Sample));

    [Fact]
    public void GoldenFileDeserializes() => Assert.Equal(Sample, GlobalsYamlSerializer.Deserialize(Golden()));

    [Fact]
    public void MissingVariablesMeansEmpty() => Assert.Empty(GlobalsYamlSerializer.Deserialize("version: 1\n"));

    [Theory]
    [InlineData("version: 2\nvariables: []\n")]
    [InlineData("variables: []\n")]
    [InlineData("variables: [unclosed\n")]
    public void InvalidFilesThrowStorageException(string yaml) =>
        Assert.Throws<StorageException>(() => GlobalsYamlSerializer.Deserialize(yaml));

    [Fact]
    public async Task MissingFileLoadsEmpty() =>
        Assert.Empty(await new FileGlobalsStore(_root).LoadAsync(TestContext.Current.CancellationToken));

    [Fact]
    public async Task SaveThenLoadRoundTripsAndOverwrites()
    {
        var store = new FileGlobalsStore(_root);
        await store.SaveAsync(Sample, TestContext.Current.CancellationToken);
        Assert.Equal(Sample, await store.LoadAsync(TestContext.Current.CancellationToken));

        await store.SaveAsync([new("a", "b")], TestContext.Current.CancellationToken);
        Assert.Equal([new KeyValueEntry("a", "b")], await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CorruptFileThrowsOnLoad()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(FilePath, "variables: [unclosed\n", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StorageException>(() => new FileGlobalsStore(_root).LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveLeavesNoTempFileAndWritesPlainLf()
    {
        await new FileGlobalsStore(_root).SaveAsync(Sample, TestContext.Current.CancellationToken);

        Assert.Equal(["globals.yaml"], Directory.GetFiles(_root).Select(Path.GetFileName));
        var bytes = await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
    }
}
