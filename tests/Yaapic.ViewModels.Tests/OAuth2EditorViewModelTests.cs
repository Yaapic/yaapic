using System.Globalization;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class OAuth2EditorViewModelTests
{
    private static readonly OAuth2Settings Sample = new(
        OAuth2Grant.Password, "https://t/token", "https://t/auth", "id", "{{secret}}", "a b", "aud", "u", "{{pw}}",
        "http://127.0.0.1:9000/cb", OAuth2ClientAuthentication.RequestBody, OAuth2TokenPlacement.Query, "Token", "tok");

    private static OAuth2EditorViewModel Loaded(OAuth2Settings settings)
    {
        OAuth2EditorViewModel vm = new();
        vm.Load(settings);
        return vm;
    }

    [Fact]
    public void Load_then_ToSettings_round_trips()
    {
        Assert.Equal(Sample, Loaded(Sample).ToSettings());
        Assert.Equal(OAuth2Settings.Default, Loaded(OAuth2Settings.Default).ToSettings());
    }

    [Fact]
    public void Load_raises_no_Changed_and_each_edit_raises_one()
    {
        OAuth2EditorViewModel vm = new();
        var count = 0;
        vm.Changed += (_, _) => count++;
        vm.Load(Sample);
        Assert.Equal(0, count);

        vm.ClientId = "other";
        vm.Grant = OAuth2Grant.ClientCredentials;
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(OAuth2Grant.ClientCredentials, false, false, false)]
    [InlineData(OAuth2Grant.AuthorizationCode, true, true, false)]
    [InlineData(OAuth2Grant.Password, false, false, true)]
    public void Visibility_follows_the_grant(OAuth2Grant grant, bool authUrl, bool redirect, bool user)
    {
        var vm = Loaded(OAuth2Settings.Default);
        List<string?> raised = [];
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        vm.Grant = grant == OAuth2Grant.ClientCredentials ? OAuth2Grant.Password : OAuth2Grant.ClientCredentials;
        vm.Grant = grant;

        Assert.Equal(authUrl, vm.ShowAuthorizationUrl);
        Assert.Equal(redirect, vm.ShowRedirectUri);
        Assert.Equal(user, vm.ShowUserCredentials);
        Assert.True(vm.ShowClientSecret);
        Assert.Contains(nameof(OAuth2EditorViewModel.ShowUserCredentials), raised);
    }

    [Fact]
    public void Placement_picks_the_header_prefix_or_the_query_name()
    {
        var vm = Loaded(OAuth2Settings.Default);
        Assert.True(vm.ShowHeaderPrefix);
        Assert.False(vm.ShowQueryParameterName);
        vm.Placement = OAuth2TokenPlacement.Query;
        Assert.False(vm.ShowHeaderPrefix);
        Assert.True(vm.ShowQueryParameterName);
        Assert.True(vm.ShowPlacementName);
    }

    [Fact]
    public void A_literal_secret_gets_a_hint_but_a_variable_or_an_empty_value_does_not()
    {
        var vm = Loaded(OAuth2Settings.Default with { ClientSecret = "s3cret" });
        Assert.Equal([Strings.AuthSecretLiteralHint(Strings.Oauth2FieldClientSecret)], vm.Hints);

        vm.ClientSecret = "{{secret}}";
        Assert.Empty(vm.Hints);
        vm.ClientSecret = "";
        Assert.Empty(vm.Hints);
        vm.ClientSecret = "pre{{secret}}";
        Assert.Single(vm.Hints);
    }

    [Fact]
    public void The_password_is_checked_for_the_password_grant_only()
    {
        var vm = Loaded(OAuth2Settings.Default with { Password = "hunter2" });
        Assert.Empty(vm.Hints);
        vm.Grant = OAuth2Grant.Password;
        Assert.Equal([Strings.AuthSecretLiteralHint(Strings.Oauth2FieldPassword)], vm.Hints);
    }

    [Fact]
    public void An_invalid_redirect_uri_is_flagged_for_the_code_grant_only()
    {
        var vm = Loaded(OAuth2Settings.Default with { RedirectUri = "https://example.com/cb" });
        Assert.Empty(vm.Hints);
        vm.Grant = OAuth2Grant.AuthorizationCode;
        Assert.Equal([Strings.Oauth2RedirectUriInvalid], vm.Hints);
        vm.RedirectUri = "http://127.0.0.1:8742/callback";
        Assert.Empty(vm.Hints);
    }

    private sealed class UtcClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTimeOffset Now = new(2026, 3, 4, 10, 0, 0, TimeSpan.Zero);

    private static readonly OAuth2Settings Ready = OAuth2Settings.Default with { TokenUrl = "https://t/token", ClientId = "id" };

    private static OAuth2EditorViewModel WithTokens(FakeOAuth2TokenService tokens, Func<OAuth2Settings, OAuth2Settings>? resolve = null)
    {
        OAuth2EditorViewModel vm = new(tokens, resolve ?? (s => s), new UtcClock(Now));
        vm.Load(Ready);
        return vm;
    }

    private static OAuth2Token Token(string value) => new(value, "Bearer", null, null, null, Now);

    [Fact]
    public void Status_text_follows_the_three_states_and_the_Changed_event()
    {
        FakeOAuth2TokenService tokens = new();
        var vm = WithTokens(tokens);
        Assert.Equal(Strings.Oauth2NoToken, vm.TokenStatusText);
        Assert.False(vm.HasToken);

        var expires = Now.AddHours(1);
        tokens.Status = new(OAuth2TokenState.Valid, expires);
        tokens.RaiseTokensChanged();
        Assert.Equal(Strings.Oauth2TokenValidUntil(expires.ToString("g", CultureInfo.CurrentCulture)), vm.TokenStatusText);
        Assert.True(vm.HasToken);

        tokens.Status = new(OAuth2TokenState.Expired);
        tokens.RaiseTokensChanged();
        Assert.Equal(Strings.Oauth2TokenExpired, vm.TokenStatusText);
        Assert.True(vm.HasToken);

        tokens.Status = new(OAuth2TokenState.NoToken);
        vm.ClientId = "edited";
        Assert.Equal(Strings.Oauth2NoToken, vm.TokenStatusText);
        Assert.False(vm.HasToken);
    }

    [Fact]
    public void Status_changes_are_posted_to_the_creating_synchronization_context()
    {
        var previous = SynchronizationContext.Current;
        RecordingContext context = new();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            FakeOAuth2TokenService tokens = new();
            var vm = WithTokens(tokens);
            tokens.Status = new(OAuth2TokenState.Expired);
            tokens.RaiseTokensChanged();

            Assert.Equal(Strings.Oauth2NoToken, vm.TokenStatusText);
            Assert.Single(context.Posted);
            context.Posted[0]();
            Assert.Equal(Strings.Oauth2TokenExpired, vm.TokenStatusText);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class RecordingContext : SynchronizationContext
    {
        public List<Action> Posted { get; } = [];

        public override void Post(SendOrPostCallback d, object? state) => Posted.Add(() => d(state));
    }

    [Fact]
    public void Status_changes_do_not_raise_Changed()
    {
        FakeOAuth2TokenService tokens = new();
        var vm = WithTokens(tokens);
        var count = 0;
        vm.Changed += (_, _) => count++;
        tokens.Status = new(OAuth2TokenState.Expired);
        tokens.RaiseTokensChanged();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetNewToken_success_clears_the_error_and_receives_the_resolved_settings()
    {
        FakeOAuth2TokenService tokens = new() { NewTokenResult = OAuth2TokenResult.Fail("boom") };
        var vm = WithTokens(tokens, s => s with { ClientId = "resolved" });
        await vm.GetNewTokenCommand.ExecuteAsync(null);
        Assert.Equal("boom", vm.TokenError);

        tokens.NewTokenResult = OAuth2TokenResult.Ok(Token("secret-token"));
        tokens.Status = new(OAuth2TokenState.Valid, Now.AddHours(1));
        await vm.GetNewTokenCommand.ExecuteAsync(null);

        Assert.Null(vm.TokenError);
        Assert.True(vm.HasToken);
        Assert.Equal("resolved", tokens.NewTokenCalls[^1].ClientId);
        Assert.False(vm.IsGettingToken);
    }

    [Fact]
    public async Task Cancelling_GetNewToken_leaves_no_error_and_resets_IsGettingToken()
    {
        FakeOAuth2TokenService tokens = new() { NewTokenGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var vm = WithTokens(tokens);
        var running = vm.GetNewTokenCommand.ExecuteAsync(null);
        Assert.True(vm.IsGettingToken);

        vm.CancelGetTokenCommand.Execute(null);
        await running;

        Assert.Null(vm.TokenError);
        Assert.False(vm.IsGettingToken);
    }

    [Fact]
    public void Clear_clears_the_service_and_the_error()
    {
        FakeOAuth2TokenService tokens = new() { Status = new(OAuth2TokenState.Expired) };
        var vm = WithTokens(tokens);
        vm.TokenError = "old";
        Assert.True(vm.ClearTokenCommand.CanExecute(null));

        tokens.Status = new(OAuth2TokenState.NoToken);
        vm.ClearTokenCommand.Execute(null);

        Assert.Equal(1, tokens.ClearCount);
        Assert.Null(vm.TokenError);
        Assert.Equal(Strings.Oauth2NoToken, vm.TokenStatusText);
        Assert.False(vm.ClearTokenCommand.CanExecute(null));
    }

    [Fact]
    public void GetNewToken_can_execute_rules()
    {
        FakeOAuth2TokenService tokens = new();
        var vm = WithTokens(tokens);
        Assert.True(vm.GetNewTokenCommand.CanExecute(null));

        vm.TokenUrl = "";
        Assert.False(vm.GetNewTokenCommand.CanExecute(null));
        vm.TokenUrl = "https://t/token";

        vm.Grant = OAuth2Grant.AuthorizationCode;
        Assert.False(vm.GetNewTokenCommand.CanExecute(null));
        vm.AuthorizationUrl = "https://t/auth";
        Assert.True(vm.GetNewTokenCommand.CanExecute(null));

        Assert.False(new OAuth2EditorViewModel().GetNewTokenCommand.CanExecute(null));
    }

    [Fact]
    public async Task No_property_contains_the_token_value()
    {
        FakeOAuth2TokenService tokens = new() { NewTokenResult = OAuth2TokenResult.Ok(Token("secret-token")), Status = new(OAuth2TokenState.Valid, Now.AddHours(1)) };
        var vm = WithTokens(tokens);
        await vm.GetNewTokenCommand.ExecuteAsync(null);

        foreach (var property in new[] { vm.TokenStatusText, vm.TokenError, vm.ToSettings().ToString() })
        {
            Assert.DoesNotContain("secret-token", property ?? "", StringComparison.Ordinal);
        }
    }
}
