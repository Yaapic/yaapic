namespace Yaapic.Core.Storage;

/// <summary>An I/O or format error raised by a storage implementation.</summary>
/// <param name="message">The error message.</param>
/// <param name="inner">The underlying exception, if any.</param>
public sealed class StorageException(string message, Exception? inner = null) : Exception(message, inner);
