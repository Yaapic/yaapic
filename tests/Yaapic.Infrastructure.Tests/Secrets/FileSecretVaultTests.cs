using System.Text;
using System.Text.Json.Nodes;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.TestDoubles;

namespace Yaapic.Infrastructure.Tests.Secrets;

public sealed class FileSecretVaultTests : IDisposable
{
    private const string Passphrase = "correct horse battery staple";
    private const string Distinctive = "s3cr3t-DISTINCTIVE-value-42";
    private static readonly Guid Owner = Guid.Parse("6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-vault-" + Guid.NewGuid().ToString("N"));
    private readonly TempLocation _location;
    private readonly string _workspace;

    public FileSecretVaultTests()
    {
        _workspace = Path.Combine(_dir, "ws");
        _location = new TempLocation(Path.Combine(_dir, "vaults"));
        Workspace.EnsureCreated(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SecretRef Ref(string name) => new(SecretOwnerKind.Environment, Owner, name);

    private FileSecretVault Passphrased() => new(_workspace, _location, new PassphraseKeyProtectorFactory(100_000));

    private FileSecretVault Faked() => new(_workspace, _location, new FakeKeyProtectorFactory(new FakeKeyProtector("fake")));

    private string VaultPath => _location.GetVaultPath(Workspace.ReadWorkspaceId(_workspace));

    [Fact]
    public async Task NotCreatedWhenNoFile()
    {
        var vault = Passphrased();
        await vault.InitializeAsync(Ct);

        Assert.Equal(VaultState.NotCreated, vault.State);
        Assert.Null(vault.ProtectorId);
        Assert.Empty(vault.GetSecretValues());
        await Assert.ThrowsAsync<VaultException>(() => vault.SetAsync(Ref("a"), "x", Ct));
    }

    [Fact]
    public async Task PassphraseRoundTrip()
    {
        var vault = Passphrased();
        var changes = 0;
        vault.StateChanged += (_, _) => changes++;
        await vault.InitializeAsync(Ct);
        await vault.CreateAsync("passphrase", Passphrase, Ct);
        await vault.SetAsync(Ref("token"), Distinctive, Ct);

        Assert.Equal(VaultState.Unlocked, vault.State);
        Assert.Equal("passphrase", vault.ProtectorId);
        Assert.Equal([Distinctive], vault.GetSecretValues());

        vault.Lock();
        Assert.Equal(VaultState.Locked, vault.State);
        Assert.False(vault.TryGet(Ref("token"), out var gone));
        Assert.Equal("", gone);
        Assert.Empty(vault.GetSecretValues());

        Assert.False(await vault.UnlockAsync("wrong", Ct));
        Assert.Equal(VaultState.Locked, vault.State);
        Assert.True(await vault.UnlockAsync(Passphrase, Ct));
        Assert.True(vault.TryGet(Ref("token"), out var value));
        Assert.Equal(Distinctive, value);
        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task RestartReadsTheSameFile()
    {
        var first = Passphrased();
        await first.CreateAsync("passphrase", Passphrase, Ct);
        await first.SetAsync(Ref("token"), Distinctive, Ct);

        var second = Passphrased();
        await second.InitializeAsync(Ct);
        Assert.Equal(VaultState.Locked, second.State);
        Assert.Equal("passphrase", second.ProtectorId);
        Assert.True(await second.UnlockAsync(Passphrase, Ct));
        Assert.True(second.TryGet(Ref("token"), out var value));
        Assert.Equal(Distinctive, value);
    }

    [Fact]
    public async Task ProtectorWithoutPassphraseUnlocksOnInitialize()
    {
        var first = Faked();
        await first.CreateAsync("fake", null, Ct);
        await first.SetAsync(Ref("token"), Distinctive, Ct);

        var second = Faked();
        await second.InitializeAsync(Ct);

        Assert.Equal(VaultState.Unlocked, second.State);
        Assert.True(second.TryGet(Ref("token"), out var value));
        Assert.Equal(Distinctive, value);
    }

    [Fact]
    public async Task UnavailableProtectorLeavesTheVaultLocked()
    {
        var first = Faked();
        await first.CreateAsync("fake", null, Ct);

        var second = new FileSecretVault(_workspace, _location, new FakeKeyProtectorFactory());
        await second.InitializeAsync(Ct);

        Assert.Equal(VaultState.Locked, second.State);
    }

    [Fact]
    public async Task FileHoldsNeitherValuesNorPassphrase()
    {
        var vault = Passphrased();
        await vault.CreateAsync("passphrase", Passphrase, Ct);
        await vault.SetAsync(Ref("token"), Distinctive, Ct);

        var bytes = await File.ReadAllBytesAsync(VaultPath, Ct);
        Assert.Equal(-1, bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Distinctive)));
        Assert.Equal(-1, bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Passphrase)));
        Assert.Contains("\"pbkdf2-sha512\"", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task TamperedEntryIsDroppedAndReported()
    {
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);
        await vault.SetAsync(Ref("good"), "good-value", Ct);
        await vault.SetAsync(Ref("bad"), "bad-value", Ct);
        Edit(root => Entry(root, "bad")["tag"] = Convert.ToBase64String(new byte[16]));

        var reloaded = Faked();
        await reloaded.InitializeAsync(Ct);

        Assert.Equal(VaultState.Unlocked, reloaded.State);
        Assert.True(reloaded.TryGet(Ref("good"), out _));
        Assert.False(reloaded.TryGet(Ref("bad"), out _));
        var warning = Assert.Single(reloaded.LoadWarnings);
        Assert.Contains("'bad'", warning);
        Assert.DoesNotContain("bad-value", warning);
    }

    [Fact]
    public async Task SwappedEntriesFailTheAadCheck()
    {
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);
        await vault.SetAsync(Ref("a"), "value-a", Ct);
        await vault.SetAsync(Ref("b"), "value-b", Ct);
        Edit(root =>
        {
            var source = Entry(root, "a");
            var target = Entry(root, "b");
            foreach (var field in new[] { "nonce", "ciphertext", "tag" })
            {
                target[field] = source[field]!.GetValue<string>();
            }
        });

        var reloaded = Faked();
        await reloaded.InitializeAsync(Ct);

        Assert.True(reloaded.TryGet(Ref("a"), out _));
        Assert.False(reloaded.TryGet(Ref("b"), out _));
        Assert.Single(reloaded.LoadWarnings);
    }

    [Theory]
    [InlineData("{ not json", "The vault file is corrupt")]
    [InlineData("{\"version\": 99}", "Unsupported vault version")]
    [InlineData("null", "The vault file is corrupt")]
    public async Task UnreadableFileStaysLockedAndUntouched(string content, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(VaultPath)!);
        await File.WriteAllTextAsync(VaultPath, content, Ct);

        var vault = Faked();
        var ex = await Assert.ThrowsAsync<VaultException>(() => vault.InitializeAsync(Ct));

        Assert.Equal(message, ex.Message);
        Assert.Equal(VaultState.Locked, vault.State);
        Assert.Equal(content, await File.ReadAllTextAsync(VaultPath, Ct));
        await Assert.ThrowsAsync<VaultException>(() => vault.CreateAsync("fake", null, Ct));
    }

    [Fact]
    public async Task RemoveAndRemoveOwner()
    {
        var other = new SecretRef(SecretOwnerKind.Globals, Guid.Empty, "g");
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);
        await vault.SetAsync(Ref("a"), "value-a", Ct);
        await vault.SetAsync(Ref("b"), "value-b", Ct);
        await vault.SetAsync(other, "value-g", Ct);

        await vault.RemoveAsync(Ref("a"), Ct);
        await vault.RemoveAsync(Ref("missing"), Ct);
        Assert.False(vault.TryGet(Ref("a"), out _));
        Assert.True(vault.TryGet(Ref("b"), out _));

        await vault.RemoveOwnerAsync(SecretOwnerKind.Environment, Owner, Ct);

        var reloaded = Faked();
        await reloaded.InitializeAsync(Ct);
        Assert.False(reloaded.TryGet(Ref("b"), out _));
        Assert.True(reloaded.TryGet(other, out var g));
        Assert.Equal("value-g", g);
    }

    [Fact]
    public async Task ConcurrentSetsAllPersist()
    {
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);

        await Task.WhenAll(Enumerable.Range(0, 25).Select(i => vault.SetAsync(Ref("k" + i), "value-" + i, Ct)));

        var reloaded = Faked();
        await reloaded.InitializeAsync(Ct);
        for (var i = 0; i < 25; i++)
        {
            Assert.True(reloaded.TryGet(Ref("k" + i), out var value));
            Assert.Equal("value-" + i, value);
        }
    }

    [Fact]
    public async Task FailedWriteKeepsCacheAndFileConsistent()
    {
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);
        await vault.SetAsync(Ref("a"), "value-a", Ct);
        var before = await File.ReadAllBytesAsync(VaultPath, Ct);
        Directory.CreateDirectory(VaultPath + ".tmp");

        await Assert.ThrowsAsync<VaultException>(() => vault.SetAsync(Ref("b"), "value-b", Ct));

        Assert.False(vault.TryGet(Ref("b"), out _));
        Assert.True(vault.TryGet(Ref("a"), out _));
        Assert.Equal(before, await File.ReadAllBytesAsync(VaultPath, Ct));
    }

    [Fact]
    public async Task CreateTwiceFailsAndPassphraseIsRequired()
    {
        var vault = Passphrased();
        await Assert.ThrowsAsync<VaultException>(() => vault.CreateAsync("passphrase", "", Ct));
        await Assert.ThrowsAsync<VaultException>(() => vault.CreateAsync("unknown", null, Ct));
        await vault.CreateAsync("passphrase", Passphrase, Ct);
        await Assert.ThrowsAsync<VaultException>(() => vault.CreateAsync("passphrase", Passphrase, Ct));
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task FileModeIsOwnerOnlyOnUnix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only");
        var vault = Faked();
        await vault.CreateAsync("fake", null, Ct);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(VaultPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(VaultPath)!));
    }

    [Fact]
    public void WorkspaceIdIsCreatedThenKept()
    {
        var root = Path.Combine(_dir, "other");
        var id = Workspace.ReadWorkspaceId(root);

        Assert.True(Guid.TryParseExact(id, "D", out _));
        Assert.Equal(id + "\n", File.ReadAllText(Path.Combine(root, ".yaapic", "workspace.id")));
        Assert.Equal(id, Workspace.ReadWorkspaceId(root));
        Workspace.EnsureCreated(root);
        Assert.Equal(id, Workspace.ReadWorkspaceId(root));
    }

    [Fact]
    public void DefaultLocationUsesTheWorkspaceId()
    {
        var id = Guid.NewGuid().ToString("D");
        var path = new DefaultVaultLocation().GetVaultPath(id);

        Assert.EndsWith(Path.Combine("Yaapic", "vaults", id + ".vault"), path);
        Assert.Throws<FormatException>(() => new DefaultVaultLocation().GetVaultPath("../evil"));
    }

    private JsonObject Entry(JsonNode root, string name) =>
        root["entries"]!.AsArray().Select(e => e!.AsObject()).Single(e => e["name"]!.GetValue<string>() == name);

    private void Edit(Action<JsonNode> change)
    {
        var root = JsonNode.Parse(File.ReadAllText(VaultPath))!;
        change(root);
        File.WriteAllText(VaultPath, root.ToJsonString());
    }

    private sealed class TempLocation(string directory) : IVaultLocation
    {
        public string GetVaultPath(string workspaceId) => Path.Combine(directory, workspaceId + ".vault");
    }
}
