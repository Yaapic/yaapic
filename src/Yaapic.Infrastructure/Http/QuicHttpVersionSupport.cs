using System.Net.Quic;
using System.Runtime.Versioning;
using Yaapic.Core.Http;

namespace Yaapic.Infrastructure.Http;

/// <summary>Reports HTTP/3 support from the presence of the QUIC library (msquic).</summary>
public sealed class QuicHttpVersionSupport : IHttpVersionSupport
{
    private readonly Lazy<bool> _supported = new(Probe);

    /// <inheritdoc/>
    public bool IsHttp3Supported => _supported.Value;

    [SupportedOSPlatformGuard("windows")]
    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macos")]
    private static bool Probe()
    {
        try
        {
            return QuicConnection.IsSupported;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
