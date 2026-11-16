using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Settings;

/// <summary>One editable client certificate entry; its passphrase is write-only and never kept after a save.</summary>
public sealed partial class CertificateItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Issues), nameof(IsValid))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Issues), nameof(IsValid))]
    private string _hostPattern;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Issues), nameof(IsValid), nameof(IsKeyPathVisible))]
    private ClientCertificateFormat _format;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Issues), nameof(IsValid))]
    private string _certificatePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Issues), nameof(IsValid))]
    private string _keyPath;

    [ObservableProperty]
    private string _passphrase = "";

    [ObservableProperty]
    private bool _removePassphrase;

    [ObservableProperty]
    private bool _hasPassphrase;

    /// <summary>Creates the view model from a stored entry.</summary>
    /// <param name="certificate">The entry.</param>
    public CertificateItemViewModel(ClientCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        Id = certificate.Id;
        _name = certificate.Name;
        _hostPattern = certificate.HostPattern;
        _format = certificate.Format;
        _certificatePath = certificate.CertificatePath;
        _keyPath = certificate.KeyPath;
        _hasPassphrase = certificate.HasPassphrase;
    }

    /// <summary>Gets the stable identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the formats the user can pick.</summary>
    public IReadOnlyList<ClientCertificateFormat> Formats { get; } = [ClientCertificateFormat.Pfx, ClientCertificateFormat.Pem];

    /// <summary>Gets a value indicating whether the key path applies, which is the case for PEM.</summary>
    public bool IsKeyPathVisible => Format == ClientCertificateFormat.Pem;

    /// <summary>Gets the localized problems of the entry; empty when it is valid.</summary>
    public IReadOnlyList<string> Issues => [.. ClientCertificateValidator.Validate(ToModel()).Select(IssueText)];

    /// <summary>Gets a value indicating whether the entry has no issue.</summary>
    public bool IsValid => ClientCertificateValidator.Validate(ToModel()).Count == 0;

    /// <summary>Builds the entry to store, without the passphrase.</summary>
    /// <returns>The entry, with <see cref="HasPassphrase"/> as it is now.</returns>
    public ClientCertificate ToModel() => new(Id, Name.Trim(), HostPattern.Trim(), Format, CertificatePath, KeyPath, HasPassphrase);

    private static string IssueText(ClientCertificateIssue issue) => issue switch
    {
        ClientCertificateIssue.NameEmpty => Strings.CertificateIssueNameEmpty,
        ClientCertificateIssue.HostPatternInvalid => Strings.CertificateIssueHostPatternInvalid,
        ClientCertificateIssue.CertificatePathEmpty => Strings.CertificateIssueCertificatePathEmpty,
        _ => Strings.CertificateIssueKeyPathRequired,
    };
}
