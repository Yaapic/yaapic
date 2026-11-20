namespace Yaapic.Core.Export;

/// <summary>A file produced by an exporter.</summary>
/// <param name="Path">The relative path, with <c>/</c> separators; also the suggested file name for a single file.</param>
/// <param name="Content">The file content.</param>
public sealed record ExportFile(string Path, byte[] Content);
