using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Tools;

/// <summary>Backs the "Decode JWT" dialog: decodes the pasted token on every change. The signature is never verified.</summary>
/// <param name="time">The clock used for the expiry state and the relative texts.</param>
public sealed partial class JwtDecoderViewModel(TimeProvider time) : ObservableObject
{
    [ObservableProperty]
    private string _input = "";

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _errorText = "";

    [ObservableProperty]
    private string _headerText = "";

    [ObservableProperty]
    private string _payloadText = "";

    [ObservableProperty]
    private string _signatureText = "";

    [ObservableProperty]
    private string _algorithmText = "";

    [ObservableProperty]
    private IReadOnlyList<JwtClaimRow> _claims = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private JwtStatusKind _statusKind;

    /// <summary>Gets the permanent note saying the signature is not verified.</summary>
    public string NotVerifiedNote => Strings.JwtNotVerifiedNote;

    /// <summary>Resets the input and every output.</summary>
    [RelayCommand]
    private void Clear() => Input = "";

    partial void OnInputChanged(string value) => Decode(value);

    private void Decode(string value)
    {
        IsValid = false;
        ErrorText = HeaderText = PayloadText = SignatureText = AlgorithmText = StatusText = "";
        Claims = [];
        StatusKind = JwtStatusKind.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!JwtDecoder.TryDecode(value, out var jwt, out var error) || jwt is null)
        {
            ErrorText = error switch
            {
                _ when error!.StartsWith("A JWT needs", StringComparison.Ordinal) => Strings.JwtDecodeErrorParts,
                _ when error.EndsWith("JSON object", StringComparison.Ordinal) => Strings.JwtDecodeErrorNotObject,
                _ => Strings.JwtDecodeErrorEncoding,
            };
            return;
        }

        var now = time.GetUtcNow();
        HeaderText = jwt.HeaderJson;
        PayloadText = jwt.PayloadJson;
        AlgorithmText = jwt.Algorithm ?? "";
        SignatureText = jwt.Signature.Length == 0 ? Strings.JwtUnsigned : $"{Strings.JwtSignatureNotVerified}: {jwt.Signature}";
        var rows = new List<JwtClaimRow>(3);
        AddRow(rows, "iat", jwt.IssuedAt, now);
        AddRow(rows, "nbf", jwt.NotBefore, now);
        AddRow(rows, "exp", jwt.ExpiresAt, now);
        Claims = rows;

        StatusKind = JwtDecoder.GetStatus(jwt, now) switch
        {
            JwtStatus.Expired => JwtStatusKind.Expired,
            JwtStatus.NotYetValid => JwtStatusKind.NotYetValid,
            JwtStatus.NoExpiry => JwtStatusKind.NoExpiry,
            _ => JwtStatusKind.Valid,
        };
        StatusText = StatusKind switch
        {
            JwtStatusKind.Expired => Strings.JwtStatusExpired(RelativeTime.Format(jwt.ExpiresAt!.Value - now)),
            JwtStatusKind.NotYetValid => Strings.JwtStatusNotYetValid(RelativeTime.Format(jwt.NotBefore!.Value - now)),
            JwtStatusKind.NoExpiry => Strings.JwtStatusNoExpiry,
            _ => Strings.JwtStatusValid(RelativeTime.Format(jwt.ExpiresAt!.Value - now)),
        };
        IsValid = true;
    }

    private void AddRow(List<JwtClaimRow> rows, string name, DateTimeOffset? at, DateTimeOffset now)
    {
        if (at is { } t)
        {
            var local = TimeZoneInfo.ConvertTime(t, time.LocalTimeZone);
            rows.Add(new JwtClaimRow(name, local.ToString("G", CultureInfo.CurrentCulture), RelativeTime.Format(t - now)));
        }
    }
}
