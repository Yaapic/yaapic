namespace Yaapic.Core.Import;

/// <summary>Thrown when an input cannot be read at all in the expected format.</summary>
public sealed class ImportException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ImportException"/> class.</summary>
    public ImportException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImportException"/> class.</summary>
    /// <param name="message">The message.</param>
    public ImportException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImportException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public ImportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
