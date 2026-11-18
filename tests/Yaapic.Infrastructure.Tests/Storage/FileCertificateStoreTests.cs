using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;
using Yaapic.TestDoubles;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class FileCertificateStoreTests : IDisposable
{
    private static readonly ClientCertificate Pfx = new(Guid.NewGuid(), "Corp", "*.example.com", ClientCertificateFormat.Pfx, "/c/corp.pfx", "", true);
    private static readonly ClientCertificate Pem = new(Guid.NewGuid(), "Lab", "lab.local:8443", ClientCertificateFormat.Pem, "/c/lab.pem", "/c/lab.key", false);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-cert-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "Yaapic", "certificates.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task WriteRawAsync(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, json, Ct);
    }

    [Fact]
    public async Task Two_entries_round_trip()
    {
        await new FileCertificateStore(_dir).SaveAsync([Pfx, Pem], Ct);

        var loaded = await new FileCertificateStore(_dir).LoadAsync(Ct);

        Assert.Equal([Pfx, Pem], loaded);
        var text = await File.ReadAllTextAsync(FilePath, Ct);
        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"format\": \"pfx\"", text);
        Assert.Contains("\"hostPattern\"", text);
        Assert.False((await File.ReadAllBytesAsync(FilePath, Ct)).AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]));
    }

    [Fact]
    public async Task A_missing_file_loads_as_empty() =>
        Assert.Empty(await new FileCertificateStore(_dir).LoadAsync(Ct));

    [Fact]
    public async Task A_corrupt_file_throws_and_is_left_untouched()
    {
        await WriteRawAsync("{ not json");

        var ex = await Assert.ThrowsAsync<StorageException>(() => new FileCertificateStore(_dir).LoadAsync(Ct));

        Assert.Contains("not valid JSON", ex.Message);
        Assert.Equal("{ not json", await File.ReadAllTextAsync(FilePath, Ct));
    }

    [Theory]
    [InlineData("""{ "version": 2, "certificates": [] }""")]
    [InlineData("""{ "certificates": [] }""")]
    public async Task A_wrong_version_throws_and_is_left_untouched(string json)
    {
        await WriteRawAsync(json);

        var ex = await Assert.ThrowsAsync<StorageException>(() => new FileCertificateStore(_dir).LoadAsync(Ct));

        Assert.Contains("version", ex.Message);
        Assert.Equal(json, await File.ReadAllTextAsync(FilePath, Ct));
    }

    [Fact]
    public async Task An_unknown_format_drops_that_entry()
    {
        var id = Guid.NewGuid();
        await WriteRawAsync($$"""
            { "version": 1, "certificates": [
              { "id": "{{id}}", "name": "Ok", "hostPattern": "a.com", "format": "pem", "certificatePath": "/a.pem", "keyPath": "", "hasPassphrase": false },
              { "id": "{{Guid.NewGuid()}}", "name": "Bad", "hostPattern": "b.com", "format": "jks", "certificatePath": "/b.jks", "keyPath": "", "hasPassphrase": false }
            ] }
            """);

        var loaded = await new FileCertificateStore(_dir).LoadAsync(Ct);

        Assert.Equal(id, Assert.Single(loaded).Id);
    }

    [Fact]
    public async Task A_failed_write_keeps_the_old_file()
    {
        var store = new FileCertificateStore(_dir);
        await store.SaveAsync([Pfx], Ct);
        var before = await File.ReadAllTextAsync(FilePath, Ct);
        Directory.CreateDirectory(FilePath + ".tmp"); // the temp file cannot be created

        await Assert.ThrowsAsync<StorageException>(() => store.SaveAsync([Pfx, Pem], Ct));

        Assert.Equal(before, await File.ReadAllTextAsync(FilePath, Ct));
    }

    [Fact]
    public async Task Changed_is_raised_once_per_successful_save()
    {
        var store = new FileCertificateStore(_dir);
        var count = 0;
        store.Changed += (_, _) => count++;

        await store.SaveAsync([Pfx], Ct);
        await store.SaveAsync([Pfx, Pem], Ct);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Changed_is_not_raised_when_the_save_fails()
    {
        var store = new FileCertificateStore(_dir);
        Directory.CreateDirectory(FilePath + ".tmp");
        var count = 0;
        store.Changed += (_, _) => count++;

        await Assert.ThrowsAsync<StorageException>(() => store.SaveAsync([Pfx], Ct));

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task The_in_memory_store_follows_the_same_contract()
    {
        var store = new InMemoryCertificateStore();
        var count = 0;
        store.Changed += (_, _) => count++;
        Assert.Empty(await store.LoadAsync(Ct));

        await store.SaveAsync([Pfx, Pem], Ct);

        Assert.Equal([Pfx, Pem], await store.LoadAsync(Ct));
        Assert.Equal(1, count);
    }
}
