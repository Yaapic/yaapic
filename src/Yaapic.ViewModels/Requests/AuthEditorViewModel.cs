using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    /// <summary>Raised on every property change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the authentication kinds offered by the editor.</summary>
    public static IReadOnlyList<AuthKind> Kinds { get; } = [AuthKind.None, AuthKind.Basic, AuthKind.Bearer, AuthKind.ApiKey];

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
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Builds the domain authentication from the current state.</summary>
    /// <returns>The authentication.</returns>
    public RequestAuth ToAuth() => new(Kind, Username, Password, Token, Key, Value, Location);

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
