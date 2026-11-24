using System.Text;
using Yaapic.Core.Import;
using Yaapic.Infrastructure.Import;

namespace Yaapic.Infrastructure.Tests.Import;

public sealed class FileImportInputReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("yaapic-import-").FullName;
    private readonly FileImportInputReader _reader = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string content = "{}")
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task A_utf8_file_with_a_bom_is_read_without_the_bom()
    {
        var path = Path.Combine(_dir, "col.json");
        await File.WriteAllBytesAsync(path, [.. Encoding.UTF8.GetPreamble(), .. "{\"a\":1}"u8.ToArray()], TestContext.Current.CancellationToken);

        var input = await _reader.ReadFileAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("col", input.Name);
        Assert.Equal("col.json", input.Single.Path);
        Assert.Equal("{\"a\":1}", input.Single.Text);
    }

    [Fact]
    public async Task A_missing_file_throws()
    {
        await Assert.ThrowsAsync<ImportException>(() => _reader.ReadFileAsync(Path.Combine(_dir, "nope.json"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_file_over_50_MiB_throws()
    {
        var path = Path.Combine(_dir, "big.json");
        await using (var f = File.Create(path))
        {
            f.SetLength(51L * 1024 * 1024);
        }

        await Assert.ThrowsAsync<ImportException>(() => _reader.ReadFileAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Invalid_utf8_bytes_are_replaced()
    {
        var path = Path.Combine(_dir, "bad.json");
        await File.WriteAllBytesAsync(path, [0x7B, 0xFF, 0xFE, 0x7D], TestContext.Current.CancellationToken);

        var input = await _reader.ReadFileAsync(path, TestContext.Current.CancellationToken);

        Assert.StartsWith("{", input.Single.Text, StringComparison.Ordinal);
        Assert.Contains('�', input.Single.Text);
    }

    [Fact]
    public async Task A_folder_is_listed_in_order_with_slash_paths_and_filtered_by_extension()
    {
        Write("b.json");
        Write("A.yaml");
        Write("sub/c.bru");
        Write("sub/d.yml");
        Write("notes.txt");
        Write("img.png");

        var input = await _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFileName(_dir), input.Name);
        Assert.Equal(["A.yaml", "b.json", "sub/c.bru", "sub/d.yml"], input.Files.Select(f => f.Path));
    }

    [Fact]
    public async Task Hidden_git_and_node_modules_directories_are_skipped()
    {
        Write("keep.json");
        Write(".git/x.json");
        Write(".hidden/y.json");
        Write("node_modules/z.json");
        Write("sub/node_modules/w.json");

        var input = await _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken);

        Assert.Equal(["keep.json"], input.Files.Select(f => f.Path));
    }

    [Fact]
    public async Task More_than_5000_files_throws()
    {
        for (var i = 0; i < 5001; i++)
        {
            File.WriteAllText(Path.Combine(_dir, $"f{i}.bru"), "");
        }

        await Assert.ThrowsAsync<ImportException>(() => _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_empty_folder_throws()
    {
        await Assert.ThrowsAsync<ImportException>(() => _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_folder_throws()
    {
        await Assert.ThrowsAsync<ImportException>(() => _reader.ReadFolderAsync(Path.Combine(_dir, "nope"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_oversize_file_in_a_folder_is_skipped()
    {
        Write("ok.json");
        await using (var f = File.Create(Path.Combine(_dir, "big.json")))
        {
            f.SetLength(51L * 1024 * 1024);
        }

        var input = await _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken);

        Assert.Equal(["ok.json"], input.Files.Select(f => f.Path));
    }

    [Fact]
    public async Task A_symbolic_link_is_skipped()
    {
        var target = Write("target.json");
        Write("real.json");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_dir, "link.json"), target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return; // no privilege to create links on this host
        }

        var input = await _reader.ReadFolderAsync(_dir, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(input.Files, f => f.Path == "link.json");
    }

    [Fact]
    public async Task Cancellation_is_honoured()
    {
        Write("a.json");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _reader.ReadFolderAsync(_dir, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _reader.ReadFileAsync(Path.Combine(_dir, "a.json"), cts.Token));
    }
}
