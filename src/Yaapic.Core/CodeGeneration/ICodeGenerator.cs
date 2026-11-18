namespace Yaapic.Core.CodeGeneration;

/// <summary>Turns a <see cref="CodeRequest"/> into source text; pure and deterministic.</summary>
public interface ICodeGenerator
{
    /// <summary>Gets the stable identifier, for example <c>curl</c>.</summary>
    string Id { get; }

    /// <summary>Gets the name shown to the user.</summary>
    string DisplayName { get; }

    /// <summary>Gets the editor language id used for highlighting.</summary>
    string EditorLanguage { get; }

    /// <summary>Generates the source text.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The source text, ending with a newline.</returns>
    string Generate(CodeRequest request);
}
