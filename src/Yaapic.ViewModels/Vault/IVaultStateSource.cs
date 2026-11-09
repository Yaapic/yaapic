using Yaapic.Core.Secrets;

namespace Yaapic.ViewModels.Vault;

/// <summary>Reports the state of the secret vault to the view models that edit secret rows.</summary>
public interface IVaultStateSource
{
    /// <summary>Raised after <see cref="State"/> changed.</summary>
    event EventHandler? StateChanged;

    /// <summary>Gets the current vault state.</summary>
    VaultState State { get; }
}
