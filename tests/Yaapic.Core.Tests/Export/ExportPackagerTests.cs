using System.IO.Compression;
using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Import;

namespace Yaapic.Core.Tests.Export;

public class ExportPackagerTests
{
    private static ExportResult Result(params string[] paths) =>
        new([.. paths.Select(p => new ExportFile(p, Encoding.UTF8.GetBytes("c:" + p)))], new ImportReport());

    [Fact]
    public void SingleFileIsReturnedAsIs()
    {
        var (name, content) = ExportPackager.Package(Result("a/b.json"), "Whatever", ".json");
        Assert.Equal("a/b.json", name);
        Assert.Equal("c:a/b.json", Encoding.UTF8.GetString(content));
    }

    [Fact]
    public void SeveralFilesBecomeADeterministicZip()
    {
        var r = Result("one.txt", "d/two.txt", "three.txt");
        var (name, bytes) = ExportPackager.Package(r, "My API (v2)!", ".x");
        Assert.Equal("my-api-v2.zip", name);
        Assert.Equal(bytes, ExportPackager.Package(r, "My API (v2)!", ".x").Content);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(["one.txt", "d/two.txt", "three.txt"], zip.Entries.Select(e => e.FullName));
        foreach (var e in zip.Entries)
        {
            // A zip stores a wall-clock time without offset: compare the clock reading.
            Assert.Equal(new DateTime(2000, 1, 1), e.LastWriteTime.DateTime);
            using var reader = new StreamReader(e.Open());
            Assert.Equal("c:" + e.FullName, reader.ReadToEnd());
        }
    }

    [Theory]
    [InlineData("../x.txt")]
    [InlineData("a/../x.txt")]
    [InlineData("/x.txt")]
    [InlineData("a\\x.txt")]
    public void UnsafePathsAreRefused(string path) =>
        Assert.Throws<ArgumentException>(() => ExportPackager.Package(Result("ok.txt", path), "n", ".x"));

    [Fact]
    public void NoFileThrows() =>
        Assert.Throws<InvalidOperationException>(() => ExportPackager.Package(Result(), "n", ".x"));

    [Fact]
    public void SafeFileNameFollowsTheSlugRule()
    {
        Assert.Equal("my-api-v2", ExportPackager.SafeFileName("My API (v2)!"));
        Assert.Equal("export", ExportPackager.SafeFileName(""));
        Assert.Equal(60, ExportPackager.SafeFileName(new string('a', 80)).Length);
    }

    [Fact]
    public void UniquePathAppendsACounterIgnoringCase()
    {
        var used = new HashSet<string>();
        Assert.Equal("d/a.json", ExportPackager.UniquePath("d/a.json", used));
        Assert.Equal("d/A-2.json", ExportPackager.UniquePath("d/A.json", used));
        Assert.Equal("d/a-3.json", ExportPackager.UniquePath("d/a.json", used));
        Assert.Equal("x.d/f-2", ExportPackager.UniquePath("x.d/f", new HashSet<string> { "x.d/f" }));
    }
}
