using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Responses;

/// <summary>Decodes a response body to text and pretty-prints JSON and XML.</summary>
public static class ResponseFormatter
{
    const int MaxPrettyBytes = 2 * 1024 * 1024;
    const int BinarySniffBytes = 8000;

    static readonly string[] BinaryPrefixes = ["image/", "audio/", "video/", "font/"];

    static readonly string[] BinaryTypes =
        ["application/octet-stream", "application/pdf", "application/zip", "application/gzip"];

    /// <summary>Formats a response body for the given view mode. Never throws.</summary>
    /// <param name="response">The response to format.</param>
    /// <param name="mode">Pretty or Raw.</param>
    /// <returns>The decoded body.</returns>
    public static FormattedBody Format(HttpResponseData response, BodyViewMode mode)
    {
        var body = response.Body;
        var mediaType = MediaType(response.ContentType);
        var language = LanguageOf(mediaType, body);

        if (IsBinary(body, mediaType))
        {
            return new FormattedBody("", language, true, false);
        }

        var text = Decode(body, response.ContentType);
        if (mode == BodyViewMode.Raw || body.Length > MaxPrettyBytes)
        {
            return new FormattedBody(text, language, false, false);
        }

        var pretty = language switch
        {
            "json" => PrettyJson(text),
            "xml" => PrettyXml(text),
            _ => null,
        };
        return pretty is null
            ? new FormattedBody(text, language, false, false)
            : new FormattedBody(pretty, language, false, true);
    }

    static string? MediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var semicolon = contentType.IndexOf(';');
        return (semicolon < 0 ? contentType : contentType[..semicolon]).Trim().ToLowerInvariant();
    }

    static bool IsBinary(byte[] body, string? mediaType)
    {
        if (mediaType is not null
            && (BinaryPrefixes.Any(mediaType.StartsWith) || BinaryTypes.Contains(mediaType)))
        {
            return true;
        }

        return body.AsSpan(0, Math.Min(body.Length, BinarySniffBytes)).Contains((byte)0);
    }

    static string LanguageOf(string? mediaType, byte[] body)
    {
        if (mediaType is null)
        {
            foreach (var b in body)
            {
                if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
                {
                    continue;
                }

                return b switch { (byte)'{' or (byte)'[' => "json", (byte)'<' => "xml", _ => "text" };
            }

            return "text";
        }

        if (mediaType is "application/json" or "text/json" || mediaType.EndsWith("+json", StringComparison.Ordinal))
        {
            return "json";
        }

        if (mediaType is "application/xml" or "text/xml" || mediaType.EndsWith("+xml", StringComparison.Ordinal))
        {
            return "xml";
        }

        return mediaType == "text/html" ? "html" : "text";
    }

    static string Decode(byte[] body, string? contentType)
    {
        var text = CharsetOf(contentType).GetString(body);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    static Encoding CharsetOf(string? contentType)
    {
        if (contentType is not null)
        {
            foreach (var part in contentType.Split(';').Skip(1))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim().Equals("charset", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return Encoding.GetEncoding(kv[1].Trim().Trim('"'));
                    }
                    catch (ArgumentException)
                    {
                        break;
                    }
                }
            }
        }

        return new UTF8Encoding(false);
    }

    static string? PrettyJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                IndentSize = 2,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
            {
                doc.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? PrettyXml(string text)
    {
        try
        {
            using var input = new StringReader(text);
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            var output = new StringBuilder();
            using (var writer = XmlWriter.Create(output, new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = true,
                NewLineChars = "\n",
            }))
            {
                writer.WriteNode(reader, true);
            }

            return output.ToString();
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
