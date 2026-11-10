namespace Yaapic.Infrastructure.Secrets;

/// <summary>The few Keychain operations the macOS protector needs, so the logic can be tested without a Keychain.</summary>
internal interface IKeychainApi
{
    /// <summary>Stores a generic password, replacing an existing item.</summary>
    /// <param name="service">The service name.</param>
    /// <param name="account">The account name.</param>
    /// <param name="secret">The secret bytes.</param>
    /// <returns>The <c>OSStatus</c>; 0 on success.</returns>
    int Store(string service, string account, byte[] secret);

    /// <summary>Reads a generic password.</summary>
    /// <param name="service">The service name.</param>
    /// <param name="account">The account name.</param>
    /// <param name="secret">The secret bytes on success.</param>
    /// <returns>The <c>OSStatus</c>; 0 on success.</returns>
    int Read(string service, string account, out byte[]? secret);

    /// <summary>Deletes a generic password; a missing item is not an error.</summary>
    /// <param name="service">The service name.</param>
    /// <param name="account">The account name.</param>
    /// <returns>The <c>OSStatus</c>; 0 on success or when absent.</returns>
    int Delete(string service, string account);
}
