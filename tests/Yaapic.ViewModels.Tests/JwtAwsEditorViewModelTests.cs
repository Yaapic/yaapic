using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class JwtAwsEditorViewModelTests
{
    private static readonly JwtSettings JwtSample = new(
        JwtAlgorithm.Rs384, "{{s}}", true, "{{key}}", "{\"a\":1}", "{\"kid\":\"x\"}", false, 60, JwtPlacement.Query, "Token", "tok");

    private static readonly AwsV4Settings AwsSample = new("AKIA", "{{secret}}", "{{token}}", "eu-west-1", "s3");

    private static JwtEditorViewModel Jwt(JwtSettings settings)
    {
        JwtEditorViewModel vm = new();
        vm.Load(settings);
        return vm;
    }

    [Fact]
    public void Jwt_round_trips()
    {
        Assert.Equal(JwtSample, Jwt(JwtSample).ToSettings());
        Assert.Equal(JwtSettings.Default, Jwt(JwtSettings.Default).ToSettings());
    }

    [Fact]
    public void Jwt_Changed_is_not_raised_by_Load_but_by_each_edit()
    {
        JwtEditorViewModel vm = new();
        var count = 0;
        vm.Changed += (_, _) => count++;
        vm.Load(JwtSample);
        Assert.Equal(0, count);

        vm.Payload = "{}";
        vm.Algorithm = JwtAlgorithm.Hs256;
        vm.ExpiresInSeconds = 5;
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData(JwtAlgorithm.Hs256, true)]
    [InlineData(JwtAlgorithm.Hs512, true)]
    [InlineData(JwtAlgorithm.Rs256, false)]
    [InlineData(JwtAlgorithm.Es384, false)]
    public void Jwt_flags_follow_the_algorithm(JwtAlgorithm algorithm, bool hmac)
    {
        var vm = Jwt(JwtSettings.Default with { Algorithm = algorithm });
        Assert.Equal(hmac, vm.UsesSecret);
        Assert.Equal(!hmac, vm.UsesPrivateKey);
    }

    [Fact]
    public void Jwt_flags_follow_the_placement()
    {
        var vm = Jwt(JwtSettings.Default);
        Assert.True(vm.ShowHeaderPrefix);
        Assert.False(vm.ShowQueryParameter);
        vm.Placement = JwtPlacement.Query;
        Assert.False(vm.ShowHeaderPrefix);
        Assert.True(vm.ShowQueryParameter);
        Assert.Equal(9, JwtEditorViewModel.Algorithms.Count);
        Assert.Equal(2, JwtEditorViewModel.Placements.Count);
    }

    [Fact]
    public void Jwt_hints_flag_literal_secret_or_key_for_the_algorithm_only()
    {
        var hs = Jwt(JwtSettings.Default with { Secret = "abc", PrivateKey = "pem" });
        Assert.Equal([Strings.AuthSecretLiteralHint(Strings.JwtFieldSecret)], hs.Hints);

        hs.Algorithm = JwtAlgorithm.Es256;
        Assert.Equal([Strings.AuthSecretLiteralHint(Strings.JwtFieldPrivateKey)], hs.Hints);

        Assert.Empty(Jwt(JwtSample with { Algorithm = JwtAlgorithm.Hs256 }).Hints);
        Assert.Empty(Jwt(JwtSettings.Default).Hints);
    }

    [Fact]
    public void Jwt_hints_flag_invalid_json_but_not_templates_or_empty()
    {
        Assert.Equal([Strings.JwtPayloadInvalid], Jwt(JwtSettings.Default with { Payload = "nope" }).Hints);
        Assert.Equal([Strings.JwtPayloadInvalid], Jwt(JwtSettings.Default with { Payload = "[1]" }).Hints);
        Assert.Equal([Strings.JwtHeadersInvalid], Jwt(JwtSettings.Default with { ExtraHeaders = "{bad" }).Hints);
        Assert.Empty(Jwt(JwtSettings.Default with { Payload = "{\"a\":\"{{x}}\"}" }).Hints);
        Assert.Empty(Jwt(JwtSettings.Default with { Payload = "", ExtraHeaders = "" }).Hints);
    }

    [Fact]
    public void Aws_round_trips_and_Changed_rules_hold()
    {
        AwsV4EditorViewModel vm = new();
        var count = 0;
        vm.Changed += (_, _) => count++;
        vm.Load(AwsSample);
        Assert.Equal(0, count);
        Assert.Equal(AwsSample, vm.ToSettings());

        vm.Region = "us-east-1";
        vm.Service = "ec2";
        Assert.Equal(2, count);
        Assert.Equal(AwsV4Settings.Default, Loaded(AwsV4Settings.Default).ToSettings());
    }

    [Fact]
    public void Aws_hints_flag_literal_secret_and_token_only()
    {
        Assert.Equal(
            [Strings.AuthSecretLiteralHint(Strings.AwsFieldSecretAccessKey), Strings.AuthSecretLiteralHint(Strings.AwsFieldSessionToken)],
            Loaded(AwsSample with { SecretAccessKey = "k", SessionToken = "t" }).Hints);
        Assert.Empty(Loaded(AwsSample).Hints);
        Assert.Empty(Loaded(AwsV4Settings.Default).Hints);
    }

    private static AwsV4EditorViewModel Loaded(AwsV4Settings settings)
    {
        AwsV4EditorViewModel vm = new();
        vm.Load(settings);
        return vm;
    }
}
