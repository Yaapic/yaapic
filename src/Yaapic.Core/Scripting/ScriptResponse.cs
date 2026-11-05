using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>The response as scripts see it.</summary>
/// <param name="Code">The status code.</param>
/// <param name="Status">The reason phrase.</param>
/// <param name="Headers">The headers in wire order.</param>
/// <param name="BodyText">The decoded body; empty for binary content types.</param>
/// <param name="SizeBytes">The body size in bytes.</param>
/// <param name="ResponseTimeMs">The duration in whole milliseconds.</param>
/// <param name="FinalUrl">The URL that produced the response.</param>
/// <param name="Cookies">The cookies set by the response.</param>
public sealed record ScriptResponse(
    int Code,
    string Status,
    IReadOnlyList<KeyValueEntry> Headers,
    string BodyText,
    long SizeBytes,
    long ResponseTimeMs,
    string FinalUrl,
    IReadOnlyList<ScriptCookie> Cookies)
{
    private static readonly string[] BinaryTypes =
        ["image/", "audio/", "video/", "application/octet-stream", "application/pdf", "application/zip"];

    /// <summary>Builds the script view of a response.</summary>
    /// <param name="data">The received response.</param>
    /// <returns>The snapshot.</returns>
    public static ScriptResponse FromData(HttpResponseData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var contentType = data.ContentType ?? "";
        var text = IsBinary(contentType) ? "" : GetEncoding(contentType).GetString(data.Body);
        return new ScriptResponse(
            data.StatusCode,
            data.ReasonPhrase,
            data.Headers,
            text,
            data.SizeBytes,
            (long)Math.Round(data.Duration.TotalMilliseconds),
            data.FinalUrl,
            SetCookieParser.Parse(data.Headers));
    }

    private static bool IsBinary(string contentType) =>
        BinaryTypes.Any(t => contentType.TrimStart().StartsWith(t, StringComparison.OrdinalIgnoreCase));

    private static Encoding GetEncoding(string contentType)
    {
        foreach (var part in contentType.Split(';').Skip(1))
        {
            var eq = part.IndexOf('=');
            if (eq > 0 && part[..eq].Trim().Equals("charset", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Encoding.GetEncoding(part[(eq + 1)..].Trim().Trim('"'));
                }
                catch (ArgumentException)
                {
                    break;
                }
            }
        }

        return Encoding.UTF8;
    }
}
