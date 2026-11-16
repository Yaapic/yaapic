using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable OAuth 2.0 configuration of a request, convertible to and from <see cref="OAuth2Settings"/>.</summary>
public sealed partial class OAuth2EditorViewModel : ObservableObject
{
    private readonly IOAuth2TokenService? _tokens;
    private readonly Func<OAuth2Settings, OAuth2Settings> _resolveSettings = static s => s;
    private readonly TimeProvider _time = TimeProvider.System;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAuthorizationUrl), nameof(ShowRedirectUri), nameof(ShowUserCredentials))]
    [NotifyCanExecuteChangedFor(nameof(GetNewTokenCommand))]
    private OAuth2Grant _grant;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetNewTokenCommand))]
    private string _tokenUrl = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetNewTokenCommand))]
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

    [ObservableProperty]
    private string _tokenStatusText = Strings.Oauth2NoToken;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearTokenCommand))]
    private bool _hasToken;

    [ObservableProperty]
    private string? _tokenError;

    /// <summary>Initializes a new instance of the <see cref="OAuth2EditorViewModel"/> class without token features.</summary>
    public OAuth2EditorViewModel()
    {
        GetNewTokenCommand.PropertyChanged += OnGetNewTokenChanged;
    }

    /// <summary>Initializes a new instance of the <see cref="OAuth2EditorViewModel"/> class with the token features.</summary>
    /// <param name="tokens">Gets, clears and reports the cached token.</param>
    /// <param name="resolveSettings">Substitutes the variables of the editor's request in the settings.</param>
    /// <param name="time">The clock and time zone of the status text.</param>
    // shortcut: stays subscribed to the service for its whole life, add Dispose when editors are discarded while the service lives on.
    public OAuth2EditorViewModel(IOAuth2TokenService tokens, Func<OAuth2Settings, OAuth2Settings> resolveSettings, TimeProvider time)
        : this()
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _resolveSettings = resolveSettings ?? throw new ArgumentNullException(nameof(resolveSettings));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _tokens.TokensChanged += (_, _) => RefreshTokenStatus();
    }

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

    /// <summary>Gets a value indicating whether the token is being fetched.</summary>
    public bool IsGettingToken => GetNewTokenCommand.IsRunning;

    /// <summary>Gets the command cancelling <see cref="GetNewTokenCommand"/>.</summary>
    public ICommand CancelGetTokenCommand => GetNewTokenCancelCommand;

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

        RefreshTokenStatus();
    }

    /// <summary>Builds the domain settings from the current state.</summary>
    /// <returns>The settings.</returns>
    public OAuth2Settings ToSettings() => new(
        Grant, TokenUrl, AuthorizationUrl, ClientId, ClientSecret, Scope, Audience, Username, Password, RedirectUri,
        ClientAuthentication, Placement, HeaderPrefix, QueryParameterName);

    private bool CanGetNewToken() =>
        _tokens is not null && TokenUrl.Length > 0 && (Grant != OAuth2Grant.AuthorizationCode || AuthorizationUrl.Length > 0);

    [RelayCommand(CanExecute = nameof(CanGetNewToken), IncludeCancelCommand = true)]
    private async Task GetNewTokenAsync(CancellationToken ct)
    {
        TokenError = null;
        try
        {
            var result = await _tokens!.GetNewTokenAsync(_resolveSettings(ToSettings()), ct);
            TokenError = result.Error;
        }
        catch (OperationCanceledException)
        {
            // Cancelling is not an error.
        }
        finally
        {
            RefreshTokenStatus();
        }
    }

    private bool CanClearToken() => _tokens is not null && HasToken;

    [RelayCommand(CanExecute = nameof(CanClearToken))]
    private void ClearToken()
    {
        _tokens!.Clear(_resolveSettings(ToSettings()));
        TokenError = null;
        RefreshTokenStatus();
    }

    private void OnGetNewTokenChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
        {
            OnPropertyChanged(nameof(IsGettingToken));
        }
    }

    private void RefreshTokenStatus()
    {
        if (_tokens is null)
        {
            return;
        }

        var status = _tokens.GetStatus(_resolveSettings(ToSettings()));
        HasToken = status.State != OAuth2TokenState.NoToken;
        TokenStatusText = status switch
        {
            { State: OAuth2TokenState.Expired } => Strings.Oauth2TokenExpired,
            { State: OAuth2TokenState.Valid, ExpiresAt: { } at } =>
                Strings.Oauth2TokenValidUntil(TimeZoneInfo.ConvertTime(at, _time.LocalTimeZone).ToString("g", CultureInfo.CurrentCulture)),
            { State: OAuth2TokenState.Valid } => Strings.Oauth2TokenNeverExpires,
            _ => Strings.Oauth2NoToken,
        };
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(TokenStatusText) or nameof(HasToken) or nameof(TokenError) or nameof(IsGettingToken))
        {
            return;
        }

        if (e.PropertyName is nameof(Hints) or nameof(ShowAuthorizationUrl) or nameof(ShowRedirectUri) or nameof(ShowUserCredentials)
            or nameof(ShowHeaderPrefix) or nameof(ShowQueryParameterName))
        {
            return;
        }

        OnPropertyChanged(nameof(Hints));
        if (!_loading)
        {
            RefreshTokenStatus();
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
