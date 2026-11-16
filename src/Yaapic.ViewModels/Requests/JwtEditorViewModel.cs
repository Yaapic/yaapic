using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable JWT signing configuration of a request, convertible to and from <see cref="JwtSettings"/>.</summary>
public sealed partial class JwtEditorViewModel : ObservableObject
{
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesSecret), nameof(UsesPrivateKey))]
    private JwtAlgorithm _algorithm;

    [ObservableProperty]
    private string _secret = "";

    [ObservableProperty]
    private bool _secretIsBase64;

    [ObservableProperty]
    private string _privateKey = "";

    [ObservableProperty]
    private string _payload = "";

    [ObservableProperty]
    private string _extraHeaders = "";

    [ObservableProperty]
    private bool _addIssuedAt;

    [ObservableProperty]
    private int _expiresInSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeaderPrefix), nameof(ShowQueryParameter))]
    private JwtPlacement _placement;

    [ObservableProperty]
    private string _headerPrefix = "";

    [ObservableProperty]
    private string _queryParameterName = "";

    /// <summary>Raised on every property change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the algorithms offered by the editor.</summary>
    public static IReadOnlyList<JwtAlgorithm> Algorithms { get; } = Enum.GetValues<JwtAlgorithm>();

    /// <summary>Gets the placements offered by the editor.</summary>
    public static IReadOnlyList<JwtPlacement> Placements { get; } = [JwtPlacement.Header, JwtPlacement.Query];

    /// <summary>Gets a value indicating whether the algorithm is an HMAC one and uses the secret.</summary>
    public bool UsesSecret => Algorithm is JwtAlgorithm.Hs256 or JwtAlgorithm.Hs384 or JwtAlgorithm.Hs512;

    /// <summary>Gets a value indicating whether the algorithm is an RSA or ECDSA one and uses the private key.</summary>
    public bool UsesPrivateKey => !UsesSecret;

    /// <summary>Gets a value indicating whether the header prefix applies.</summary>
    public bool ShowHeaderPrefix => Placement == JwtPlacement.Header;

    /// <summary>Gets a value indicating whether the query parameter name applies.</summary>
    public bool ShowQueryParameter => Placement == JwtPlacement.Query;

    /// <summary>Gets the warnings about the current values: literal secrets and invalid JSON.</summary>
    public IReadOnlyList<string> Hints
    {
        get
        {
            List<string> hints = [];
            if (UsesSecret && AuthHints.IsLiteralSecret(Secret))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.JwtFieldSecret));
            }

            if (UsesPrivateKey && AuthHints.IsLiteralSecret(PrivateKey))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.JwtFieldPrivateKey));
            }

            if (!IsJsonObjectOrTemplate(Payload))
            {
                hints.Add(Strings.JwtPayloadInvalid);
            }

            if (!IsJsonObjectOrTemplate(ExtraHeaders))
            {
                hints.Add(Strings.JwtHeadersInvalid);
            }

            return hints;
        }
    }

    /// <summary>Replaces the state with <paramref name="settings"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="settings">The settings to show.</param>
    public void Load(JwtSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _loading = true;
        try
        {
            Algorithm = settings.Algorithm;
            Secret = settings.Secret;
            SecretIsBase64 = settings.SecretIsBase64;
            PrivateKey = settings.PrivateKey;
            Payload = settings.Payload;
            ExtraHeaders = settings.ExtraHeaders;
            AddIssuedAt = settings.AddIssuedAt;
            ExpiresInSeconds = settings.ExpiresInSeconds;
            Placement = settings.Placement;
            HeaderPrefix = settings.HeaderPrefix;
            QueryParameterName = settings.QueryParameterName;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Builds the domain settings from the current state.</summary>
    /// <returns>The settings.</returns>
    public JwtSettings ToSettings() => new(
        Algorithm, Secret, SecretIsBase64, PrivateKey, Payload, ExtraHeaders, AddIssuedAt, ExpiresInSeconds, Placement,
        HeaderPrefix, QueryParameterName);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Hints) or nameof(UsesSecret) or nameof(UsesPrivateKey) or nameof(ShowHeaderPrefix)
            or nameof(ShowQueryParameter))
        {
            return;
        }

        OnPropertyChanged(nameof(Hints));
        if (!_loading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsJsonObjectOrTemplate(string value)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Contains("{{", StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
