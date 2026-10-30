namespace Yaapic.UI.Abstractions;

/// <summary>The answer to an unsaved-changes prompt.</summary>
public enum UnsavedChoice
{
    /// <summary>Save the changes, then continue.</summary>
    Save,

    /// <summary>Throw the changes away and continue.</summary>
    Discard,

    /// <summary>Abort the operation.</summary>
    Cancel,
}
