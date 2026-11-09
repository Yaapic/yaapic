namespace Yaapic.Core.Secrets;

/// <summary>The state of the secret vault.</summary>
public enum VaultState
{
    /// <summary>No vault file exists yet.</summary>
    NotCreated,

    /// <summary>The vault exists but its data key is not available.</summary>
    Locked,

    /// <summary>The vault is unlocked and secrets can be read and written.</summary>
    Unlocked,
}
