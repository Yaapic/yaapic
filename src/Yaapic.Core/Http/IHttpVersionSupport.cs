namespace Yaapic.Core.Http;

/// <summary>Tells which optional HTTP versions the current environment can use.</summary>
public interface IHttpVersionSupport
{
    /// <summary>Gets a value indicating whether HTTP/3 (QUIC) is available.</summary>
    bool IsHttp3Supported { get; }
}
