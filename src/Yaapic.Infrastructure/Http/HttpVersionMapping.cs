using System.Globalization;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Http;

/// <summary>Maps the requested HTTP version onto an <see cref="HttpRequestMessage"/> and the used version onto text.</summary>
internal static class HttpVersionMapping
{
    /// <summary>Sets <see cref="HttpRequestMessage.Version"/> and its policy for the preference.</summary>
    /// <param name="message">The message to configure.</param>
    /// <param name="preference">The requested version.</param>
    /// <param name="http3Supported">Whether HTTP/3 is available; when not, <see cref="HttpVersionPreference.Http3"/> is sent as HTTP/2.</param>
    internal static void Apply(HttpRequestMessage message, HttpVersionPreference preference, bool http3Supported)
    {
        (message.Version, message.VersionPolicy) = preference switch
        {
            HttpVersionPreference.Http1 => (System.Net.HttpVersion.Version11, HttpVersionPolicy.RequestVersionExact),
            HttpVersionPreference.Http2 => (System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrLower),
            HttpVersionPreference.Http3 when http3Supported => (System.Net.HttpVersion.Version30, HttpVersionPolicy.RequestVersionOrLower),
            HttpVersionPreference.Http3 => (System.Net.HttpVersion.Version20, HttpVersionPolicy.RequestVersionOrLower),
            _ => (System.Net.HttpVersion.Version11, HttpVersionPolicy.RequestVersionOrHigher),
        };
    }

    /// <summary>Formats a response version as <c>HTTP/1.1</c>, <c>HTTP/2</c> or <c>HTTP/3</c>.</summary>
    /// <param name="version">The version reported by the response.</param>
    /// <returns>The protocol text.</returns>
    internal static string ProtocolText(Version version) =>
        version.Major >= 2
            ? "HTTP/" + version.Major.ToString(CultureInfo.InvariantCulture)
            : "HTTP/" + version.ToString(2);
}
