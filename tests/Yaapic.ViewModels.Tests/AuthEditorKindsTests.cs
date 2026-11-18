using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class AuthEditorKindsTests
{
    private static readonly OAuth2Settings OAuth = OAuth2Settings.Default with { TokenUrl = "https://t/token", ClientId = "id" };
    private static readonly JwtSettings Jwt = JwtSettings.Default with { Secret = "s3cret" };
    private static readonly AwsV4Settings Aws = AwsV4Settings.Default with { AccessKeyId = "AK", Region = "eu-west-1" };

    private static RequestAuth None(AuthKind kind) => RequestAuth.None with { Kind = kind };

    public static TheoryData<RequestAuth> NewKinds => new()
    {
        None(AuthKind.OAuth2) with { OAuth2 = OAuth },
        None(AuthKind.Jwt) with { Jwt = Jwt },
        None(AuthKind.AwsV4) with { AwsV4 = Aws },
        None(AuthKind.Digest) with { Username = "u", Password = "p" },
    };

    [Fact]
    public void Kinds_lists_every_kind_in_order() =>
        Assert.Equal(
            [AuthKind.None, AuthKind.Basic, AuthKind.Bearer, AuthKind.ApiKey, AuthKind.OAuth2, AuthKind.Jwt, AuthKind.Digest, AuthKind.AwsV4],
            AuthEditorViewModel.Kinds);

    [Theory]
    [MemberData(nameof(NewKinds))]
    public void Load_then_ToAuth_round_trips(RequestAuth auth)
    {
        AuthEditorViewModel vm = new();
        vm.Load(auth);
        Assert.Equal(auth, vm.ToAuth());
    }

    [Fact]
    public void ToAuth_leaves_the_other_kinds_null()
    {
        AuthEditorViewModel vm = new();
        vm.Load(None(AuthKind.Jwt) with { Jwt = Jwt, OAuth2 = OAuth, AwsV4 = Aws });
        var auth = vm.ToAuth();
        Assert.Equal(Jwt, auth.Jwt);
        Assert.Null(auth.OAuth2);
        Assert.Null(auth.AwsV4);
    }

    [Fact]
    public void Switching_kinds_keeps_the_in_memory_edits()
    {
        AuthEditorViewModel vm = new();
        vm.Load(None(AuthKind.OAuth2) with { OAuth2 = OAuth });
        vm.OAuth2.ClientId = "edited";
        vm.Kind = AuthKind.Jwt;
        Assert.Null(vm.ToAuth().OAuth2);
        vm.Kind = AuthKind.OAuth2;
        Assert.Equal("edited", vm.ToAuth().OAuth2!.ClientId);
    }

    [Fact]
    public void Changed_is_forwarded_from_each_sub_editor_and_not_raised_by_Load()
    {
        AuthEditorViewModel vm = new();
        var count = 0;
        vm.Changed += (_, _) => count++;
        vm.Load(None(AuthKind.OAuth2) with { OAuth2 = OAuth, Jwt = Jwt, AwsV4 = Aws });
        Assert.Equal(0, count);

        vm.OAuth2.ClientId = "x";
        Assert.Equal(1, count);
        vm.Jwt.Secret = "y";
        Assert.Equal(2, count);
        vm.AwsV4.Region = "z";
        Assert.Equal(3, count);
    }

    [Fact]
    public void Request_editor_is_dirty_after_an_OAuth2_edit_but_not_after_load()
    {
        var definition = HttpRequestDefinition.Create("r") with { Auth = None(AuthKind.OAuth2) with { OAuth2 = OAuth } };
        var editor = RequestEditorViewModel.FromDefinition(definition, new(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "x")))), new FakeVariableScopeProvider());
        Assert.False(editor.IsDirty);
        editor.Auth.OAuth2.ClientId = "other";
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public async Task Get_token_receives_the_settings_with_variables_substituted()
    {
        FakeOAuth2TokenService tokens = new() { NewTokenResult = OAuth2TokenResult.Fail("nope") };
        var env = new EnvironmentDefinition(Guid.NewGuid(), "e", [new KeyValueEntry("host", "https://auth.test")]);
        var definition = HttpRequestDefinition.Create("r") with
        {
            Auth = None(AuthKind.OAuth2) with { OAuth2 = OAuth with { TokenUrl = "{{host}}/token" } },
        };
        var editor = RequestEditorViewModel.FromDefinition(
            definition,
            new(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "x")))),
            new FakeVariableScopeProvider { Scopes = new VariableScopes(env, [], []) },
            oauth2Tokens: tokens);

        await editor.Auth.OAuth2.GetNewTokenCommand.ExecuteAsync(null);

        Assert.Equal("https://auth.test/token", Assert.Single(tokens.NewTokenCalls).TokenUrl);
    }
}
