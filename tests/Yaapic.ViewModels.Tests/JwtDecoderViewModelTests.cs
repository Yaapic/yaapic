using System.Text;
using Yaapic.Localization;
using Yaapic.ViewModels.Tools;

namespace Yaapic.ViewModels.Tests;

public sealed class JwtDecoderViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Token(string payload, string sig = "c2ln", string header = """{"alg":"HS256","typ":"JWT"}""") =>
        $"{B64(header)}.{B64(payload)}.{sig}";

    private static string Claim(string name, TimeSpan offset) => $"\"{name}\":{(Now + offset).ToUnixTimeSeconds()}";

    private static JwtDecoderViewModel Vm(string input = "")
    {
        var vm = new JwtDecoderViewModel(new FakeTime(Now));
        vm.Input = input;
        return vm;
    }

    [Fact]
    public void Valid_token_fills_every_output()
    {
        var vm = Vm(Token("{" + Claim("iat", -TimeSpan.FromHours(1)) + "," + Claim("nbf", -TimeSpan.FromHours(1)) + "," + Claim("exp", TimeSpan.FromHours(2)) + "}"));

        Assert.True(vm.IsValid);
        Assert.Equal("", vm.ErrorText);
        Assert.Contains("HS256", vm.HeaderText);
        Assert.Contains("iat", vm.PayloadText);
        Assert.Equal("HS256", vm.AlgorithmText);
        Assert.Equal($"{Strings.JwtSignatureNotVerified}: c2ln", vm.SignatureText);
        Assert.Equal(["iat", "nbf", "exp"], vm.Claims.Select(c => c.Name));
        Assert.Equal([Strings.RelativeInPast(Strings.RelativeHours(1)), Strings.RelativeInPast(Strings.RelativeHours(1)), Strings.RelativeInFuture(Strings.RelativeHours(2))], vm.Claims.Select(c => c.RelativeText));
        Assert.Equal(new DateTime(2026, 6, 1, 14, 0, 0).ToString("G", System.Globalization.CultureInfo.CurrentCulture), vm.Claims[2].LocalTimeText);
        Assert.Equal(JwtStatusKind.Valid, vm.StatusKind);
        Assert.Equal(Strings.JwtStatusValid(Strings.RelativeInFuture(Strings.RelativeHours(2))), vm.StatusText);
        Assert.False(string.IsNullOrEmpty(vm.NotVerifiedNote));
    }

    [Fact]
    public void Bearer_prefix_is_accepted()
    {
        Assert.True(Vm("Bearer " + Token("{}")).IsValid);
    }

    [Fact]
    public void Empty_input_gives_no_error_and_no_status()
    {
        var vm = Vm("   ");

        Assert.False(vm.IsValid);
        Assert.Equal("", vm.ErrorText);
        Assert.Equal(JwtStatusKind.None, vm.StatusKind);
    }

    [Theory]
    [InlineData("abc.def")]
    [InlineData("a.b.c.d")]
    public void Wrong_part_count_is_reported(string input)
    {
        var vm = Vm(input);

        Assert.False(vm.IsValid);
        Assert.Equal(Strings.JwtDecodeErrorParts, vm.ErrorText);
    }

    [Fact]
    public void Bad_encoding_is_reported()
    {
        var vm = Vm("!!!." + B64("{}") + ".x");

        Assert.Equal(Strings.JwtDecodeErrorEncoding, vm.ErrorText);
        Assert.Equal(JwtStatusKind.None, vm.StatusKind);
    }

    [Theory]
    [InlineData("[1]", "{}")]
    [InlineData("{}", "42")]
    public void Non_object_parts_are_reported(string header, string payload)
    {
        var vm = Vm($"{B64(header)}.{B64(payload)}.x");

        Assert.Equal(Strings.JwtDecodeErrorNotObject, vm.ErrorText);
    }

    [Fact]
    public void Unsigned_token_says_so()
    {
        var vm = Vm(Token("{}", sig: ""));

        Assert.True(vm.IsValid);
        Assert.Equal(Strings.JwtUnsigned, vm.SignatureText);
        Assert.Equal(JwtStatusKind.NoExpiry, vm.StatusKind);
        Assert.Equal(Strings.JwtStatusNoExpiry, vm.StatusText);
    }

    [Fact]
    public void Expired_and_boundary()
    {
        var past = Vm(Token("{" + Claim("exp", -TimeSpan.FromDays(3)) + "}"));
        Assert.Equal(JwtStatusKind.Expired, past.StatusKind);
        Assert.Equal(Strings.JwtStatusExpired(Strings.RelativeInPast(Strings.RelativeDays(3))), past.StatusText);

        var exactly = Vm(Token("{" + Claim("exp", TimeSpan.Zero) + "}"));
        Assert.Equal(JwtStatusKind.Expired, exactly.StatusKind);
        Assert.Equal(Strings.JwtStatusExpired(Strings.RelativeNow), exactly.StatusText);

        var oneSecond = Vm(Token("{" + Claim("exp", TimeSpan.FromSeconds(1)) + "}"));
        Assert.Equal(JwtStatusKind.Valid, oneSecond.StatusKind);
    }

    [Fact]
    public void Not_yet_valid()
    {
        var vm = Vm(Token("{" + Claim("nbf", TimeSpan.FromMinutes(5)) + "," + Claim("exp", TimeSpan.FromDays(1)) + "}"));

        Assert.Equal(JwtStatusKind.NotYetValid, vm.StatusKind);
        Assert.Equal(Strings.JwtStatusNotYetValid(Strings.RelativeInFuture(Strings.RelativeMinutes(5))), vm.StatusText);
    }

    [Fact]
    public void Clear_resets_everything()
    {
        var vm = Vm(Token("{}"));

        vm.ClearCommand.Execute(null);

        Assert.Equal("", vm.Input);
        Assert.False(vm.IsValid);
        Assert.Equal("", vm.HeaderText);
        Assert.Empty(vm.Claims);
        Assert.Equal(JwtStatusKind.None, vm.StatusKind);
    }

    [Theory]
    [InlineData(0, "now")]
    [InlineData(59, "in 59 s")]
    [InlineData(60, "in 1 min")]
    [InlineData(3599, "in 59 min")]
    [InlineData(3600, "in 1 h")]
    [InlineData(86399, "in 23 h")]
    [InlineData(86400, "in 1 day")]
    [InlineData(172800, "in 2 days")]
    [InlineData(-172800, "2 days ago")]
    [InlineData(-30, "30 s ago")]
    public void Relative_time_boundaries(int seconds, string expected)
    {
        Localizer.Use(UiLanguage.English);

        Assert.Equal(expected, RelativeTime.Format(TimeSpan.FromSeconds(seconds)));
    }
}
