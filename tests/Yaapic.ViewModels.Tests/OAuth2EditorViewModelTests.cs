using Yaapic.Domain.Http;
using Yaapic.Localization;
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
}
