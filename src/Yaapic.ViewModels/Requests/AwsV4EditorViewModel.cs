using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Requests;

/// <summary>Editable AWS Signature Version 4 configuration of a request, convertible to and from <see cref="AwsV4Settings"/>.</summary>
public sealed partial class AwsV4EditorViewModel : ObservableObject
{
    private bool _loading;

    [ObservableProperty]
    private string _accessKeyId = "";

    [ObservableProperty]
    private string _secretAccessKey = "";

    [ObservableProperty]
    private string _sessionToken = "";

    [ObservableProperty]
    private string _region = "";

    [ObservableProperty]
    private string _service = "";

    /// <summary>Raised on every property change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the warnings about the current values: literal secrets.</summary>
    public IReadOnlyList<string> Hints
    {
        get
        {
            List<string> hints = [];
            if (AuthHints.IsLiteralSecret(SecretAccessKey))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.AwsFieldSecretAccessKey));
            }

            if (AuthHints.IsLiteralSecret(SessionToken))
            {
                hints.Add(Strings.AuthSecretLiteralHint(Strings.AwsFieldSessionToken));
            }

            return hints;
        }
    }

    /// <summary>Replaces the state with <paramref name="settings"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="settings">The settings to show.</param>
    public void Load(AwsV4Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _loading = true;
        try
        {
            AccessKeyId = settings.AccessKeyId;
            SecretAccessKey = settings.SecretAccessKey;
            SessionToken = settings.SessionToken;
            Region = settings.Region;
            Service = settings.Service;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Builds the domain settings from the current state.</summary>
    /// <returns>The settings.</returns>
    public AwsV4Settings ToSettings() => new(AccessKeyId, SecretAccessKey, SessionToken, Region, Service);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Hints))
        {
            return;
        }

        OnPropertyChanged(nameof(Hints));
        if (!_loading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
