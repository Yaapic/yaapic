namespace Yaapic.Core.Import;

/// <summary>Reads a YAML document into a <see cref="DataNode"/> tree.</summary>
public interface IYamlTreeReader
{
    /// <summary>Parses a YAML document.</summary>
    /// <param name="yaml">The YAML text.</param>
    /// <returns>The tree.</returns>
    /// <exception cref="FormatException">The text is not valid YAML; the message carries the line and column.</exception>
    DataNode Parse(string yaml);
}
