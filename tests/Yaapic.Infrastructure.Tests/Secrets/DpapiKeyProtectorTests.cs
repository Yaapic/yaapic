using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Secrets;

public sealed class DpapiKeyProtectorTests : IDisposable
{
    private const string Skip = "DPAPI is only available on Windows";
    private const string Distinctive = "s3cr3t-DISTINCTIVE-value-42";
    private static readonly Guid Owner = Guid.Parse("6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8");
    private static readonly string WorkspaceA = Guid.NewGuid().ToString();
    private static readonly string WorkspaceB = Guid.NewGuid().ToString();

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-dpapi-" + Guid.NewGuid().ToString("N"));

    public static bool IsWindows => OperatingSystem.IsWindows();

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Skip = Skip, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiKeyProtectorTests))]
    public void WrapUnwrapRoundTrip()
    {
        var protector = new DpapiKeyProtector();
        byte[] key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

        var wrapped = protector.Wrap(key, WorkspaceA);

        Assert.True(protector.IsAvailable(out var reason));
        Assert.Null(reason);
        Assert.False(protector.RequiresPassphrase);
        Assert.NotEqual(key, wrapped.Bytes);
        Assert.Equal(key, protector.Unwrap(wrapped, WorkspaceA));
    }

    [Fact(Skip = Skip, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiKeyProtectorTests))]
    public void BlobIsBoundToTheWorkspace()
    {
        var protector = new DpapiKeyProtector();
        var wrapped = protector.Wrap(new byte[32], WorkspaceA);

        Assert.Throws<VaultException>(() => protector.Unwrap(wrapped, WorkspaceB));
    }

    [Fact(Skip = Skip, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiKeyProtectorTests))]
    public void TamperedBlobFails()
    {
        var protector = new DpapiKeyProtector();
        var bytes = protector.Wrap(new byte[32], WorkspaceA).Bytes.ToArray();
        bytes[^1] ^= 0xFF;

        Assert.Throws<VaultException>(() => protector.Unwrap(new WrappedKey(bytes, null), WorkspaceA));
    }

    [Fact(Skip = Skip, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiKeyProtectorTests))]
    public void FactoryListsDpapiFirst()
    {
        var factory = new DpapiKeyProtectorFactory(1_000);

        Assert.Equal(["dpapi", "passphrase"], factory.Available().Select(p => p.Id));
        Assert.IsType<DpapiKeyProtector>(factory.Create("dpapi", null));
        Assert.IsType<PassphraseKeyProtector>(factory.Create("passphrase", "x"));
        Assert.Null(factory.Create("unknown", null));
    }

    [Fact(Skip = Skip, SkipUnless = nameof(IsWindows), SkipType = typeof(DpapiKeyProtectorTests))]
    public async Task VaultAutoUnlocksAfterRestart()
    {
        var workspace = Path.Combine(_dir, "ws");
        Workspace.EnsureCreated(workspace);
        var location = new Location(Path.Combine(_dir, "vaults"));
        var secret = new SecretRef(SecretOwnerKind.Environment, Owner, "token");
        var factory = new DpapiKeyProtectorFactory(1_000);

        var first = new FileSecretVault(workspace, location, factory);
        await first.CreateAsync("dpapi", null, Ct);
        await first.SetAsync(secret, Distinctive, Ct);
        Assert.Equal(VaultState.Unlocked, first.State);

        var second = new FileSecretVault(workspace, location, factory);
        await second.InitializeAsync(Ct);

        Assert.Equal(VaultState.Unlocked, second.State);
        Assert.Equal("dpapi", second.ProtectorId);
        Assert.True(second.TryGet(secret, out var value));
        Assert.Equal(Distinctive, value);
    }

    [Fact]
    public void FactoryOffersOnlyPassphraseOffWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var factory = new DpapiKeyProtectorFactory(1_000);

        Assert.Equal(["passphrase"], factory.Available().Select(p => p.Id));
        Assert.Null(factory.Create("dpapi", null));
        Assert.False(new DpapiKeyProtector().IsAvailable(out var reason));
        Assert.NotNull(reason);
    }

    private sealed class Location(string directory) : IVaultLocation
    {
        public string GetVaultPath(string workspaceId) => Path.Combine(directory, workspaceId + ".vault");
    }
}
