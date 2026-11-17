using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class OAuth2TokenCacheTests
{
    static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    static OAuth2Settings Settings() => OAuth2Settings.Default with
    {
        TokenUrl = "https://t/token",
        ClientId = "id",
        Scope = "a b",
        Audience = "aud",
        Username = "u",
    };

    static OAuth2Token Token(string access = "at", DateTimeOffset? exp = null) =>
        new(access, "Bearer", "rt", "a", exp, T0);

    [Fact]
    public void Key_is_stable_lowercase_hex_and_ignores_secrets()
    {
        var key = OAuth2TokenKey.For(Settings());
        Assert.Equal(key, OAuth2TokenKey.For(Settings()));
        Assert.Matches("^[0-9a-f]{64}$", key.Value);
        Assert.Equal(key, OAuth2TokenKey.For(Settings() with { ClientSecret = "s", Password = "p" }));
    }

    [Fact]
    public void Key_changes_with_each_included_field()
    {
        var key = OAuth2TokenKey.For(Settings());
        OAuth2Settings[] variants =
        [
            Settings() with { Grant = OAuth2Grant.Password },
            Settings() with { TokenUrl = "x" },
            Settings() with { ClientId = "x" },
            Settings() with { Scope = "x" },
            Settings() with { Audience = "x" },
            Settings() with { Username = "x" },
        ];
        Assert.All(variants, v => Assert.NotEqual(key, OAuth2TokenKey.For(v)));
    }

    [Fact]
    public void Expiry_honours_leeway_and_null()
    {
        var at = T0.AddMinutes(1);
        Assert.False(Token(exp: at).IsExpired(T0, TimeSpan.FromSeconds(30)));
        Assert.True(Token(exp: at).IsExpired(T0.AddSeconds(30), TimeSpan.FromSeconds(30)));
        Assert.True(Token(exp: at).IsExpired(at, TimeSpan.Zero));
        Assert.False(Token().IsExpired(DateTimeOffset.MaxValue.AddDays(-2), TimeSpan.FromDays(1)));
        Assert.Equal(TimeSpan.FromSeconds(30), OAuth2Token.DefaultLeeway);
    }

    [Fact]
    public void Set_get_remove_clear_and_changed()
    {
        var cache = new InMemoryOAuth2TokenCache();
        var changes = 0;
        cache.Changed += (_, _) => changes++;
        var key = OAuth2TokenKey.For(Settings());

        Assert.False(cache.TryGet(key, out _));
        cache.Set(key, Token(exp: T0.AddDays(-1)));
        Assert.True(cache.TryGet(key, out var got));
        Assert.Equal("at", got.AccessToken);
        Assert.Equal(1, changes);

        cache.Remove(key);
        cache.Remove(key);
        Assert.False(cache.TryGet(key, out _));
        Assert.Equal(2, changes);

        cache.Clear();
        Assert.Equal(2, changes);
        cache.Set(key, Token());
        cache.Clear();
        Assert.False(cache.TryGet(key, out _));
        Assert.Equal(4, changes);
    }

    [Fact]
    public async Task Concurrent_sets_are_safe()
    {
        var cache = new InMemoryOAuth2TokenCache();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                cache.Set(new OAuth2TokenKey($"{t}-{i}"), Token());
            }
        })));
        for (var t = 0; t < 8; t++)
        {
            Assert.True(cache.TryGet(new OAuth2TokenKey($"{t}-199"), out _));
        }
    }

    [Fact]
    public void ToString_redacts_tokens()
    {
        var text = new OAuth2Token("secret-at", "Bearer", "secret-rt", null, null, T0).ToString();
        Assert.Contains("AccessToken = ****", text);
        Assert.DoesNotContain("secret-at", text);
        Assert.DoesNotContain("secret-rt", text);
    }
}
