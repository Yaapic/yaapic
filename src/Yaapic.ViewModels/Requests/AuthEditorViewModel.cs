using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable authentication of a request, convertible to and from <see cref="RequestAuth"/>.</summary>
public sealed partial class AuthEditorViewModel : ObservableObject
{
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private AuthKind _kind;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _token = "";

    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private ApiKeyLocation _location;

    /// <summary>Initializes a new instance of the <see cref="AuthEditorViewModel"/> class without OAuth 2.0 token features.</summary>
    public AuthEditorViewModel()
        : this(new OAuth2EditorViewModel())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AuthEditorViewModel"/> class with the OAuth 2.0 token features.</summary>
    /// <param name="tokens">Gets, clears and reports the cached token.</param>
    /// <param name="resolveSettings">Substitutes the variables of the editor's request in the OAuth 2.0 settings.</param>
    /// <param name="time">The clock and time zone of the token status text.</param>
    public AuthEditorViewModel(IOAuth2TokenService tokens, Func<OAuth2Settings, OAuth2Settings> resolveSettings, TimeProvider time)
        : this(new OAuth2EditorViewModel(tokens, resolveSettings, time))
    {
    }

    private AuthEditorViewModel(OAuth2EditorViewModel oauth2)
    {
        OAuth2 = oauth2;
        OAuth2.Changed += OnSubEditorChanged;
        Jwt.Changed += OnSubEditorChanged;
        AwsV4.Changed += OnSubEditorChanged;
    }

    /// <summary>Raised on every property change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the authentication kinds offered by the editor.</summary>
    public static IReadOnlyList<AuthKind> Kinds { get; } = [
        AuthKind.None, AuthKind.Basic, AuthKind.Bearer, AuthKind.ApiKey, AuthKind.OAuth2, AuthKind.Jwt, AuthKind.Digest, AuthKind.AwsV4,
    ];

    /// <summary>Gets the OAuth 2.0 editor.</summary>
    public OAuth2EditorViewModel OAuth2 { get; }

    /// <summary>Gets the JWT editor.</summary>
    public JwtEditorViewModel Jwt { get; } = new();

    /// <summary>Gets the AWS Signature v4 editor.</summary>
    public AwsV4EditorViewModel AwsV4 { get; } = new();

    /// <summary>Gets the API key locations offered by the editor.</summary>
    public static IReadOnlyList<ApiKeyLocation> Locations { get; } = [ApiKeyLocation.Header, ApiKeyLocation.Query];

    /// <summary>Gets a value indicating whether an authentication is configured.</summary>
    public bool IsActive => Kind != AuthKind.None;

    /// <summary>Replaces the state with <paramref name="auth"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="auth">The authentication to show.</param>
    public void Load(RequestAuth auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _loading = true;
        try
        {
            Kind = auth.Kind;
            Username = auth.Username;
            Password = auth.Password;
            Token = auth.Token;
            Key = auth.Key;
            Value = auth.Value;
            Location = auth.Location;
            OAuth2.Load(auth.OAuth2 ?? OAuth2Settings.Default);
            Jwt.Load(auth.Jwt ?? JwtSettings.Default);
            AwsV4.Load(auth.AwsV4 ?? AwsV4Settings.Default);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Builds the domain authentication from the current state.</summary>
    /// <returns>The authentication.</returns>
    public RequestAuth ToAuth() => new(Kind, Username, Password, Token, Key, Value, Location)
    {
        OAuth2 = Kind == AuthKind.OAuth2 ? OAuth2.ToSettings() : null,
        Jwt = Kind == AuthKind.Jwt ? Jwt.ToSettings() : null,
        AwsV4 = Kind == AuthKind.AwsV4 ? AwsV4.ToSettings() : null,
    };

    private void OnSubEditorChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName != nameof(IsActive))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
