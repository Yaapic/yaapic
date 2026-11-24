namespace Yaapic.Core.Export;

/// <summary>Thrown when a source cannot be exported at all.</summary>
/// <param name="message">The message.</param>
public sealed class ExportException(string message) : Exception(message);
