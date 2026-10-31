namespace Yaapic.Domain.Http;

/// <summary>Why a request produced no response.</summary>
public enum SendFailureKind
{
    /// <summary>The URL is not valid.</summary>
    InvalidUrl,

    /// <summary>The request timed out.</summary>
    Timeout,

    /// <summary>The connection could not be established or was lost.</summary>
    ConnectionFailed,

    /// <summary>The user canceled the request.</summary>
    Canceled,

    /// <summary>Any other failure.</summary>
    Unknown,
}
