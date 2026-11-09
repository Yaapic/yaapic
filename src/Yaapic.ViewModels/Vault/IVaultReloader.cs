namespace Yaapic.ViewModels.Vault;

/// <summary>Refreshes the secret-bearing view models (environments, globals, collection tree) after a vault state change.</summary>
public interface IVaultReloader
{
    /// <summary>Reloads everything that shows or edits secret values.</summary>
    /// <returns>A task that completes when the reload is done.</returns>
    Task ReloadAsync();
}
