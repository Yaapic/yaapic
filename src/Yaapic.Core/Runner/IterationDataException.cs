namespace Yaapic.Core.Runner;

/// <summary>Thrown when an iteration data file cannot be read; the message is localized and meant for the user.</summary>
/// <param name="message">The localized message.</param>
public sealed class IterationDataException(string message) : Exception(message);
