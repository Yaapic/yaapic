namespace Yaapic.ViewModels.Vault;

/// <summary>A key protector the user can pick when creating the vault.</summary>
/// <param name="Id">The protector identifier.</param>
/// <param name="Label">The label shown to the user.</param>
/// <param name="RequiresPassphrase">Whether the protector needs a passphrase.</param>
public sealed record VaultProtectorChoice(string Id, string Label, bool RequiresPassphrase);
