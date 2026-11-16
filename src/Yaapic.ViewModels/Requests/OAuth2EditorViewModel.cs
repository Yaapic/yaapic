using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable OAuth 2.0 configuration of a request, convertible to and from <see cref="OAuth2Settings"/>.</summary>
public sealed partial class OAuth2EditorViewModel : ObservableObject
{
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAuthorizationUrl), nameof(ShowRedirectUri), nameof(ShowUserCredentials))]
    private OAuth2Grant _grant;

    [ObservableProperty]
    private string _tokenUrl = "";

    [ObservableProperty]
    private string _authorizationUrl = "";

    [ObservableProperty]
    private string _clientId = "";

    [ObservableProperty]
    private string _clientSecret = "";

    [ObservableProperty]
    private string _scope = "";

    [ObservableProperty]
    private string _audience = "";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _redirectUri = "";

    [ObservableProperty]
    private OAuth2ClientAuthentication _clientAuthentication;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHeaderPrefix), nameof(ShowQueryParameterName))]
    private OAuth2TokenPlacement _placement;

    [ObservableProperty]
    private string _headerPrefix = "";

    [ObservableProperty]
    private string _queryParameterName = "";

    /// <summary>Raised on every property change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the grants offered by the editor.</summary>
    public static IReadOnlyList<OAuth2Grant> Grants { get; } =
        [OAuth2Grant.ClientCredentials, OAuth2Grant.AuthorizationCode, OAuth2Grant.Password];

    /// <summary>Gets the client authentication methods offered by the editor.</summary>
    public static IReadOnlyList<OAuth2ClientAuthentication> ClientAuthentications { get; } =
        [OAuth2ClientAuthentication.BasicHeader, OAuth2ClientAuthentication.RequestBody];

    /// <summary>Gets the token placements offered by the editor.</summary>
    public static IReadOnlyList<OAuth2TokenPlacement> Placements { get; } =
        [OAuth2TokenPlacement.Header, OAuth2TokenPlacement.Query];

    /// <summary>Gets a value indicating whether the authorization URL applies.</summary>
    public bool ShowAuthorizationUrl => Grant == OAuth2Grant.AuthorizationCode;

    /// <summary>Gets a value indicating whether the redirect URI applies.</summary>
    public bool ShowRedirectUri => Grant == OAuth2Grant.AuthorizationCode;

    /// <summary>Gets a value indicating whether the resource owner credentials apply.</summary>
    public bool ShowUserCredentials => Grant == OAuth2Grant.Password;

    /// <summary>Gets a value indicating whether the client secret applies (every grant).</summary>
    public bool ShowClientSecret => true;

    /// <summary>Gets a value indicating whether the placement field is shown: the header prefix or the query parameter name, always one of them.</summary>
    public bool ShowPlacementName => true;

    /// <summary>Gets a value indicating whether the placement field is the header prefix.</summary>
    public bool ShowHeaderPrefix => Placement == OAuth2TokenPlacement.Header;

    /// <summary>Gets a value indicating whether the placement field is the query parameter name.</summary>
    public bool ShowQueryParameterName => Placement == OAuth2TokenPlacement.Query;

    /// <summary>Gets the warnings about the current values: literal secrets and an invalid redirect URI.</summary>
    public IReadOnlyList<string> Hints
    {
        get
        {
            List<string> hints = [];
            if (IsLiteralSecret(ClientSecret))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.Oauth2FieldClientSecret));
            }

            if (ShowUserCredentials && IsLiteralSecret(Password))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.Oauth2FieldPassword));
            }

            if (ShowRedirectUri && !OAuth2AuthorizationRequest.TryGetListenAddress(RedirectUri, out _, out _))
            {
                hints.Add(Strings.Oauth2RedirectUriInvalid);
            }

            return hints;
        }
    }

    /// <summary>Replaces the state with <paramref name="settings"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="settings">The settings to show.</param>
    public void Load(OAuth2Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _loading = true;
        try
        {
            Grant = settings.Grant;
            TokenUrl = settings.TokenUrl;
            AuthorizationUrl = settings.AuthorizationUrl;
            ClientId = settings.ClientId;
            ClientSecret = settings.ClientSecret;
            Scope = settings.Scope;
            Audience = settings.Audience;
            Username = settings.Username;
            Password = settings.Password;
            RedirectUri = settings.RedirectUri;
            ClientAuthentication = settings.ClientAuthentication;
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
    public OAuth2Settings ToSettings() => new(
        Grant, TokenUrl, AuthorizationUrl, ClientId, ClientSecret, Scope, Audience, Username, Password, RedirectUri,
        ClientAuthentication, Placement, HeaderPrefix, QueryParameterName);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Hints) or nameof(ShowAuthorizationUrl) or nameof(ShowRedirectUri) or nameof(ShowUserCredentials)
            or nameof(ShowHeaderPrefix) or nameof(ShowQueryParameterName))
        {
            return;
        }

        OnPropertyChanged(nameof(Hints));
        if (!_loading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsLiteralSecret(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var spans = VariableScanner.ScanSpans(text);
        return !(spans.Count == 1 && spans[0].Start == 0 && spans[0].End == text.Length);
    }
}
