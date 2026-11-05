using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Yaapic.Infrastructure.Storage;

/// <summary>The source text of a script block as stored in YAML.</summary>
/// <param name="Value">The source text.</param>
internal sealed record ScriptSourceText(string Value);

/// <summary>
/// Writes script sources as plain VYaml scalars, except that a source whose first character is a space or a tab is
/// double-quoted: VYaml emits such a source as a literal block without an indentation indicator, which does not parse.
/// </summary>
internal sealed class ScriptSourceFormatter : IYamlFormatter<ScriptSourceText?>
{
    /// <summary>Serializer options that resolve <see cref="ScriptSourceText"/> to this formatter.</summary>
    public static readonly YamlSerializerOptions Options = new()
    {
        Resolver = CompositeResolver.Create([new ScriptSourceFormatter()], [StandardResolver.Instance]),
    };

    /// <inheritdoc />
    public void Serialize(ref Utf8YamlEmitter emitter, ScriptSourceText? value, YamlSerializationContext context)
    {
        if (value is null)
        {
            emitter.WriteNull();
            return;
        }

        var text = value.Value.Replace("\r\n", "\n");
        var quote = text.Length > 0 && text[0] is ' ' or '\t';
        emitter.WriteString(text, quote ? ScalarStyle.DoubleQuoted : ScalarStyle.Any);
    }

    /// <inheritdoc />
    public ScriptSourceText? Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        if (parser.IsNullScalar())
        {
            parser.Read();
            return null;
        }

        return new ScriptSourceText(parser.ReadScalarAsString() ?? "");
    }
}
