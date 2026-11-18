namespace Yaapic.Core.Import;

/// <summary>One text file of an import source.</summary>
/// <param name="Path">The path relative to the chosen root, with <c>/</c> separators; the file name for a single file.</param>
/// <param name="Text">The file content.</param>
public sealed record ImportFile(string Path, string Text);
