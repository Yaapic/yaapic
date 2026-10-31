namespace Yaapic.Core.Variables;

/// <summary>Supplies the value of a named variable.</summary>
public interface IVariableSource
{
    /// <summary>Looks a variable up by its exact, case-sensitive name.</summary>
    /// <param name="name">The variable name, already trimmed.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when the variable is defined.</returns>
    bool TryGet(string name, out string value);
}
