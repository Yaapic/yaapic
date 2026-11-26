namespace Yaapic.Core.Mocks;

/// <summary>Reason phrases of the common HTTP status codes.</summary>
public static class ReasonPhrases
{
    /// <summary>Gets the reason phrase of a status code.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The phrase, or <c>Status</c> for an unknown code.</returns>
    public static string For(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        304 => "Not Modified",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        415 => "Unsupported Media Type",
        422 => "Unprocessable Entity",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        501 => "Not Implemented",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Status",
    };
}
