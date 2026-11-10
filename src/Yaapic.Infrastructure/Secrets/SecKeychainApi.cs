using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>The real Keychain, through the legacy <c>SecKeychain*GenericPassword</c> functions (blittable P/Invokes only).</summary>
[SupportedOSPlatform("macos")]
internal sealed partial class SecKeychainApi : IKeychainApi
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int ErrSecItemNotFound = -25300;

    public int Store(string service, string account, byte[] secret)
    {
        var status = Delete(service, account);
        if (status != 0)
        {
            return status;
        }

        var (s, a) = (Encoding.UTF8.GetBytes(service), Encoding.UTF8.GetBytes(account));
        return SecKeychainAddGenericPassword(0, (uint)s.Length, s, (uint)a.Length, a, (uint)secret.Length, secret, 0);
    }

    public int Read(string service, string account, out byte[]? secret)
    {
        secret = null;
        var (s, a) = (Encoding.UTF8.GetBytes(service), Encoding.UTF8.GetBytes(account));
        var status = SecKeychainFindGenericPassword(0, (uint)s.Length, s, (uint)a.Length, a, out var length, out var data, 0);
        if (status != 0)
        {
            return status;
        }

        secret = new byte[length];
        Marshal.Copy(data, secret, 0, (int)length);
        _ = SecKeychainItemFreeContent(0, data);
        return 0;
    }

    public int Delete(string service, string account)
    {
        var (s, a) = (Encoding.UTF8.GetBytes(service), Encoding.UTF8.GetBytes(account));
        var status = SecKeychainFindGenericPassword(0, (uint)s.Length, s, (uint)a.Length, a, out _, out var data, out var item);
        if (status == ErrSecItemNotFound)
        {
            return 0;
        }

        if (status != 0)
        {
            return status;
        }

        _ = SecKeychainItemFreeContent(0, data);
        status = SecKeychainItemDelete(item);
        CFRelease(item);
        return status;
    }

    [LibraryImport(Security)]
    private static partial int SecKeychainAddGenericPassword(
        nint keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, nint itemRef);

    [LibraryImport(Security, EntryPoint = "SecKeychainFindGenericPassword")]
    private static partial int SecKeychainFindGenericPassword(
        nint keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out nint password, nint itemRef);

    [LibraryImport(Security, EntryPoint = "SecKeychainFindGenericPassword")]
    private static partial int SecKeychainFindGenericPassword(
        nint keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out nint password, out nint itemRef);

    [LibraryImport(Security)]
    private static partial int SecKeychainItemFreeContent(nint attrList, nint data);

    [LibraryImport(Security)]
    private static partial int SecKeychainItemDelete(nint item);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint cf);
}
