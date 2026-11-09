namespace Yaapic.Core.Secrets;

/// <summary>A vault failure: wrong passphrase, corrupt file or protector failure. The message is user-presentable.</summary>
/// <param name="message">The user-presentable message.</param>
/// <param name="inner">The underlying exception, if any.</param>
public sealed class VaultException(string message, Exception? inner = null) : Exception(message, inner);
