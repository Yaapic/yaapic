namespace Yaapic.Core.Import;

/// <summary>Reads JSON or YAML text, choosing the format from the first significant character.</summary>
/// <param name="yaml">The YAML reader used when the text is not JSON.</param>
public sealed class StructuredTextReader(IYamlTreeReader yaml)
{
    /// <summary>Parses the text as JSON when it starts with <c>{</c> or <c>[</c>, as YAML otherwise.</summary>
    /// <param name="text">The document text.</param>
    /// <returns>The tree.</returns>
    /// <exception cref="FormatException">The text is not valid.</exception>
    public DataNode Parse(string text)
    {
        var first = text.AsSpan().TrimStart();
        return first.Length > 0 && first[0] is '{' or '[' ? DataNodeJson.Parse(text) : yaml.Parse(text);
    }
}
