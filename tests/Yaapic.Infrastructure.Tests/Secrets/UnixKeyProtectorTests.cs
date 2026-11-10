using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;

namespace Yaapic.Infrastructure.Tests.Secrets;

public sealed class UnixKeyProtectorTests
{
    private static readonly byte[] Key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    public static bool IsMacOS => OperatingSystem.IsMacOS();

    [Fact]
    public void KeychainWrapStoresTheKeyAndReturnsAMarker()
    {
        var api = new FakeKeychain();
        var protector = new MacKeychainKeyProtector(api);

        var wrapped = protector.Wrap(Key, "ws1");

        Assert.Equal("keychain-v1"u8.ToArray(), wrapped.Bytes);
        Assert.Equal(("Yaapic", "ws1"), api.Items.Keys.Single());
        Assert.Equal(Key, protector.Unwrap(wrapped, "ws1"));
        Assert.False(protector.RequiresPassphrase);
        Assert.Equal("keychain", protector.Id);
    }

    [Fact]
    public void KeychainWrapReplacesAnExistingItem()
    {
        var api = new FakeKeychain();
        var protector = new MacKeychainKeyProtector(api);
        protector.Wrap(new byte[32], "ws1");

        var wrapped = protector.Wrap(Key, "ws1");

        Assert.Equal(Key, protector.Unwrap(wrapped, "ws1"));
        Assert.Single(api.Items);
    }

    [Fact]
    public void KeychainErrorsBecomeVaultExceptions()
    {
        var protector = new MacKeychainKeyProtector(new FakeKeychain { Status = -128 });

        var wrap = Assert.Throws<VaultException>(() => protector.Wrap(Key, "ws1"));
        Assert.Equal("The macOS Keychain refused access", wrap.Message);
        var unwrap = Assert.Throws<VaultException>(() => protector.Unwrap(new WrappedKey("keychain-v1"u8.ToArray(), null), "ws1"));
        Assert.Equal("The macOS Keychain refused access", unwrap.Message);
    }

    [Fact]
    public void KeychainRejectsMissingItemsBadMarkersAndBadKeyLengths()
    {
        var api = new FakeKeychain();
        var protector = new MacKeychainKeyProtector(api);
        var marker = new WrappedKey("keychain-v1"u8.ToArray(), null);

        Assert.Throws<VaultException>(() => protector.Unwrap(marker, "missing"));
        Assert.Throws<VaultException>(() => protector.Unwrap(new WrappedKey([1, 2], null), "ws1"));
        api.Items[("Yaapic", "short")] = [1, 2, 3];
        Assert.Throws<VaultException>(() => protector.Unwrap(marker, "short"));
    }

    [Fact]
    public void KeychainIsUnavailableOffMacOs()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        var protector = new MacKeychainKeyProtector();

        Assert.False(protector.IsAvailable(out var reason));
        Assert.Contains("macOS", reason);
        Assert.Throws<VaultException>(() => protector.Wrap(Key, "ws1"));
    }

    [Fact(Skip = "Needs the real macOS Keychain", SkipUnless = nameof(IsMacOS), SkipType = typeof(UnixKeyProtectorTests))]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public void KeychainRealRoundTrip()
    {
        var workspace = "Yaapic.Test." + Guid.NewGuid();
        var protector = new MacKeychainKeyProtector();
        try
        {
            var wrapped = protector.Wrap(Key, workspace);

            Assert.Equal(Key, protector.Unwrap(wrapped, workspace));
        }
        finally
        {
            new SecKeychainApi().Delete("Yaapic", workspace);
        }
    }

    [Fact]
    public void SecretToolStoresBase64OnStdinOnly()
    {
        var tool = new FakeTool();
        var protector = new SecretToolKeyProtector(tool, skipOsCheck: true);

        var wrapped = protector.Wrap(Key, "ws1");

        Assert.Equal("secret-service-v1"u8.ToArray(), wrapped.Bytes);
        var call = tool.Calls.Single();
        Assert.Equal(["store", "--label", "Yaapic vault", "service", "Yaapic", "account", "ws1"], call.Args);
        Assert.Equal(Convert.ToBase64String(Key), call.Stdin);
        Assert.DoesNotContain(call.Args, a => a.Contains(Convert.ToBase64String(Key)));
        Assert.Equal(Key, protector.Unwrap(wrapped, "ws1"));
    }

    [Fact]
    public void SecretToolErrorsBecomeVaultExceptions()
    {
        var protector = new SecretToolKeyProtector(new FakeTool { Exit = 1 }, skipOsCheck: true);

        Assert.Throws<VaultException>(() => protector.Wrap(Key, "ws1"));
        Assert.Throws<VaultException>(() => protector.Unwrap(new WrappedKey("secret-service-v1"u8.ToArray(), null), "ws1"));
        Assert.Throws<VaultException>(() => protector.Unwrap(new WrappedKey([1], null), "ws1"));
    }

    [Fact]
    public void SecretToolRejectsGarbageAndShortKeys()
    {
        var marker = new WrappedKey("secret-service-v1"u8.ToArray(), null);

        Assert.Throws<VaultException>(() => new SecretToolKeyProtector(new FakeTool { Stored = "not base64!" }, true).Unwrap(marker, "ws1"));
        Assert.Throws<VaultException>(() => new SecretToolKeyProtector(new FakeTool { Stored = Convert.ToBase64String(new byte[5]) }, true).Unwrap(marker, "ws1"));
    }

    [Fact]
    public void SecretToolAvailabilityReasons()
    {
        Assert.False(new SecretToolKeyProtector(new FakeTool { Installed = false }, true).IsAvailable(out var missing));
        Assert.Contains("secret-tool", missing);
        Assert.False(new SecretToolKeyProtector(new FakeTool { Exit = 1, ProbeExit = 2 }, true).IsAvailable(out var dbus));
        Assert.Contains("not reachable", dbus);
        Assert.False(new SecretToolKeyProtector(new FakeTool { ProbeExit = -1 }, true).IsAvailable(out _));
        Assert.True(new SecretToolKeyProtector(new FakeTool { ProbeExit = 1 }, true).IsAvailable(out var ok));
        Assert.Null(ok);
        Assert.True(new SecretToolKeyProtector(new FakeTool { ProbeExit = 0 }, true).IsAvailable(out _));
        if (!OperatingSystem.IsLinux())
        {
            Assert.False(new SecretToolKeyProtector().IsAvailable(out var os));
            Assert.Contains("Linux", os);
        }
    }

    [Fact]
    public void FactoryListsThePlatformProtectorFirstWhenAvailable()
    {
        var factory = new UnixKeyProtectorFactory(1_000, new MacKeychainKeyProtector(new FakeKeychain()));

        Assert.Equal(["keychain", "passphrase"], factory.Available().Select(p => p.Id));
        Assert.IsType<MacKeychainKeyProtector>(factory.Create("keychain", null));
        Assert.IsType<PassphraseKeyProtector>(factory.Create("passphrase", "x"));
        Assert.Null(factory.Create("unknown", null));
    }

    [Fact]
    public void FactoryFallsBackToPassphraseWhenThePlatformProtectorIsUnavailable()
    {
        var factory = new UnixKeyProtectorFactory(1_000, new SecretToolKeyProtector(new FakeTool { Installed = false }, true));

        Assert.Equal(["passphrase"], factory.Available().Select(p => p.Id));
        Assert.Null(factory.Create("secret-service", null));
        Assert.IsType<PassphraseKeyProtector>(factory.Create("passphrase", "x"));
    }

    [Fact(Skip = "The Linux CI runner has no D-Bus session for a real secret-tool; run manually on a desktop session")]
    public void SecretToolRealRoundTrip()
    {
        var protector = new SecretToolKeyProtector();
        var workspace = "Yaapic.Test." + Guid.NewGuid();

        Assert.True(protector.IsAvailable(out _));
        Assert.Equal(Key, protector.Unwrap(protector.Wrap(Key, workspace), workspace));
    }

    private sealed class FakeKeychain : IKeychainApi
    {
        public Dictionary<(string, string), byte[]> Items { get; } = [];

        public int Status { get; init; }

        public int Store(string service, string account, byte[] secret)
        {
            if (Status != 0)
            {
                return Status;
            }

            Items[(service, account)] = secret;
            return 0;
        }

        public int Read(string service, string account, out byte[]? secret)
        {
            secret = null;
            if (Status != 0)
            {
                return Status;
            }

            return Items.TryGetValue((service, account), out secret) ? 0 : -25300;
        }

        public int Delete(string service, string account)
        {
            Items.Remove((service, account));
            return 0;
        }
    }

    private sealed class FakeTool : ISecretTool
    {
        public List<(string[] Args, string? Stdin)> Calls { get; } = [];

        public bool Installed { get; init; } = true;

        public int Exit { get; init; }

        public int ProbeExit { get; init; }

        public string? Stored { get; set; }

        public bool Exists() => Installed;

        public SecretToolResult Run(IReadOnlyList<string> arguments, string? stdin)
        {
            if (arguments[^1] == "__probe__")
            {
                return new SecretToolResult(ProbeExit, "");
            }

            Calls.Add(([.. arguments], stdin));
            if (arguments[0] == "store")
            {
                Stored = stdin;
                return new SecretToolResult(Exit, "");
            }

            return new SecretToolResult(Exit, Stored ?? "");
        }
    }
}
