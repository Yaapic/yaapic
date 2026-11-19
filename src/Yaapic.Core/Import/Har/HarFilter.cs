namespace Yaapic.Core.Import.Har;

/// <summary>Why a HAR entry is not imported.</summary>
internal enum HarSkipReason
{
    /// <summary>The entry is imported.</summary>
    None,

    /// <summary>A static resource (image, font, style, media).</summary>
    Static,

    /// <summary>A CORS preflight request.</summary>
    Preflight,

    /// <summary>A WebSocket or event stream.</summary>
    Socket,

    /// <summary>A <c>data:</c> or <c>blob:</c> URL.</summary>
    DataUrl,
}

/// <summary>Decides which HAR entries are not API calls.</summary>
internal static class HarFilter
{
    private static readonly HashSet<string> StaticTypes = new(StringComparer.Ordinal)
    {
        "stylesheet", "image", "media", "font", "texttrack", "manifest",
    };

    /// <summary>Tells whether an entry is skipped, and why; the first matching reason wins.</summary>
    internal static bool ShouldSkip(DataNode entry, ImportOptions options, out HarSkipReason reason)
    {
        var resourceType = entry.GetString("_resourceType")?.ToLowerInvariant();
        var request = entry["request"];
        var url = request?.GetString("url") ?? "";
        if (!options.IncludeStaticResources && IsStatic(entry, resourceType))
        {
            reason = HarSkipReason.Static;
        }
        else if (IsPreflight(request))
        {
            reason = HarSkipReason.Preflight;
        }
        else if (resourceType is "websocket" or "eventsource"
            || url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            reason = HarSkipReason.Socket;
        }
        else if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            reason = HarSkipReason.DataUrl;
        }
        else
        {
            reason = HarSkipReason.None;
        }

        return reason != HarSkipReason.None;
    }

    private static bool IsStatic(DataNode entry, string? resourceType)
    {
        if (resourceType is not null)
        {
            return StaticTypes.Contains(resourceType);
        }

        var mime = entry["response"]?["content"]?.GetString("mimeType")?.ToLowerInvariant() ?? "";
        return mime.StartsWith("image/", StringComparison.Ordinal) || mime.StartsWith("font/", StringComparison.Ordinal)
            || mime.StartsWith("audio/", StringComparison.Ordinal) || mime.StartsWith("video/", StringComparison.Ordinal)
            || mime == "text/css";
    }

    private static bool IsPreflight(DataNode? request) =>
        request is not null
        && string.Equals(request.GetString("method"), "OPTIONS", StringComparison.OrdinalIgnoreCase)
        && (request["headers"]?.Items ?? []).Any(h =>
            string.Equals(h.GetString("name"), "Access-Control-Request-Method", StringComparison.OrdinalIgnoreCase));
}
