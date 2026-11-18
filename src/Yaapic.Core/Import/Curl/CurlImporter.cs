using System.Globalization;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Curl;

/// <summary>Turns pasted <c>curl</c> commands (bash form) into unfiled requests.</summary>
public sealed class CurlImporter : IImporter
{
    private const string NoValueGroupLetters = "sSLkivfgN012346IG";

    private static readonly Dictionary<string, string> ShortNames = new()
    {
        ["X"] = "request", ["H"] = "header", ["d"] = "data", ["F"] = "form", ["u"] = "user", ["A"] = "user-agent",
        ["e"] = "referer", ["b"] = "cookie", ["m"] = "max-time", ["E"] = "cert", ["x"] = "proxy", ["o"] = "output",
        ["T"] = "upload-file", ["w"] = "write-out", ["K"] = "config", ["I"] = "head", ["G"] = "get", ["L"] = "location",
        ["k"] = "insecure", ["s"] = "silent", ["S"] = "show-error", ["v"] = "verbose", ["i"] = "include", ["f"] = "fail",
        ["g"] = "globoff", ["N"] = "no-buffer", ["O"] = "remote-name", ["#"] = "silent", ["0"] = "http1.0",
        ["1"] = "silent", ["2"] = "silent", ["3"] = "silent", ["4"] = "silent", ["6"] = "silent",
    };

    private static readonly HashSet<string> ValueOptions =
    [
        "request", "header", "data", "data-raw", "data-ascii", "data-binary", "data-urlencode", "form", "form-string", "user",
        "oauth2-bearer", "user-agent", "referer", "cookie", "max-time", "max-redirs", "url", "cert", "key", "cacert",
        "proxy", "proxy-user", "output", "upload-file", "write-out", "resolve", "connect-timeout", "retry", "config", "interface",
    ];

    /// <inheritdoc />
    public string Id => "curl";

    /// <inheritdoc />
    public string DisplayName => Strings.ImportCurlDisplayName;

    /// <inheritdoc />
    public ImportConfidence Sniff(ImportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Files.Count == 0)
        {
            return ImportConfidence.None;
        }

        var text = input.Files[0].Text.AsSpan().TrimStart();
        if (text.StartsWith("$ ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart();
        }

        var end = text.IndexOfAny(" \t\r\n");
        var first = end < 0 ? text : text[..end];
        if (first.SequenceEqual("curl") || first.SequenceEqual("curl.exe"))
        {
            return ImportConfidence.Certain;
        }

        foreach (var line in input.Files[0].Text.Split('\n'))
        {
            if (line.TrimStart().StartsWith("curl ", StringComparison.Ordinal))
            {
                return ImportConfidence.Possible;
            }
        }

        return ImportConfidence.None;
    }

    /// <inheritdoc />
    public ImportBundle Import(ImportInput input, ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<IReadOnlyList<string>> commands;
        try
        {
            commands = CurlTokenizer.Tokenize(input.Single.Text);
        }
        catch (FormatException ex)
        {
            throw new ImportException(ex.Message, ex);
        }

        if (commands.Count == 0)
        {
            throw new ImportException(Strings.ImportCurlNoCommand);
        }

        List<HttpRequestDefinition> requests = [];
        foreach (var words in commands)
        {
            if (Convert(words, context) is { } request)
            {
                requests.Add(request);
            }
        }

        return new ImportBundle(null, requests, [], [], context.Report);
    }

    private static HttpRequestDefinition? Convert(IReadOnlyList<string> words, ImportContext context)
    {
        var cmd = new Command();
        for (var i = 1; i < words.Count; i++)
        {
            i = ReadWord(cmd, words, i);
        }

        if (cmd.Url.Length == 0)
        {
            context.Report.Skipped("curl", Strings.ImportCurlNoUrl);
            return null;
        }

        var (url, query) = ImportHelpers.SplitUrl(cmd.Url);
        if (cmd.MethodText is not null && !ImportHelpers.TryParseMethod(cmd.MethodText, out _))
        {
            Flush(cmd, url, context);
            context.Report.Skipped(url, Strings.ImportMethodUnsupported(cmd.MethodText));
            return null;
        }

        var hasBody = cmd.Data.Count > 0 || cmd.UrlEncoded.Count > 0 || cmd.Form.Count > 0 || cmd.BinaryPath is not null || cmd.Upload;
        var method = HttpMethodKind.Get;
        if (cmd.MethodText is not null)
        {
            ImportHelpers.TryParseMethod(cmd.MethodText, out method);
        }
        else if (cmd.Head)
        {
            method = HttpMethodKind.Head;
        }
        else if (!cmd.Get && hasBody)
        {
            method = HttpMethodKind.Post;
        }

        List<KeyValueEntry> queryParams = [.. query];
        var body = RequestBody.None;
        if (cmd.Get)
        {
            foreach (var piece in string.Join('&', cmd.Data).Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var (key, value) = SplitPair(piece);
                queryParams.Add(new KeyValueEntry(key, value));
            }

            queryParams.AddRange(cmd.UrlEncoded.Select(f => new KeyValueEntry(f.Key, f.Value)));
        }
        else
        {
            body = BuildBody(cmd);
        }

        if (!cmd.Get && cmd.Form.Count > 0)
        {
            cmd.Headers.RemoveAll(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                && h.Value.TrimStart().StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase));
        }

        if (body.Kind == BodyKind.Raw && body.ContentType == RawContentType.Text && !HasContentType(cmd.Headers))
        {
            cmd.Headers.Add(new KeyValueEntry("Content-Type", "application/x-www-form-urlencoded"));
        }

        var name = ImportHelpers.DescribeRequest(method, url);
        Flush(cmd, name, context);
        if (body.Kind == BodyKind.Binary)
        {
            context.Report.Warning(name, Strings.ImportFileReference(name, body.FilePath));
        }

        foreach (var field in body.Fields.Where(f => f.Kind == FormFieldKind.File))
        {
            context.Report.Warning(name, Strings.ImportFileReference(name, field.Value));
        }

        var auth = cmd.Bearer is not null
            ? RequestAuth.None with { Kind = AuthKind.Bearer, Token = cmd.Bearer }
            : cmd.User is null
                ? RequestAuth.None
                : RequestAuth.None with { Kind = cmd.Digest ? AuthKind.Digest : AuthKind.Basic, Username = cmd.User, Password = cmd.Secret };

        return new HttpRequestDefinition(
            context.NewId(), name, method, url, queryParams, cmd.Headers, body, auth,
            new RequestSettings(cmd.Follow, cmd.Timeout, cmd.Version));
    }

    private static void Flush(Command cmd, string path, ImportContext context)
    {
        foreach (var (severity, message) in cmd.Notes)
        {
            context.Report.Add(severity, path, message);
        }
    }

    private static bool HasContentType(List<KeyValueEntry> headers) =>
        headers.Exists(h => h.Enabled && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));

    private static RequestBody BuildBody(Command cmd)
    {
        if (cmd.Form.Count > 0)
        {
            return RequestBody.FormData(cmd.Form);
        }

        if (cmd.BinaryPath is not null)
        {
            return RequestBody.Binary(cmd.BinaryPath);
        }

        if (cmd.Data.Count == 0)
        {
            return cmd.UrlEncoded.Count > 0 ? RequestBody.UrlEncoded(cmd.UrlEncoded) : RequestBody.None;
        }

        var data = new List<string>(cmd.Data);
        data.AddRange(cmd.UrlEncoded.Select(f => Uri.EscapeDataString(f.Key) + "=" + Uri.EscapeDataString(f.Value)));
        var text = string.Join('&', data);
        if (text.Length == 0)
        {
            return RequestBody.None;
        }

        var contentType = cmd.Headers.Find(h => h.Enabled && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        if (contentType is null)
        {
            return IsFormShaped(text, strict: true) ? RequestBody.UrlEncoded(ParseFields(text)) : RequestBody.Raw(RawContentType.Text, text);
        }

        if (contentType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            && !ContainsAny(contentType, "json", "xml", "html", "javascript"))
        {
            return IsFormShaped(text, strict: false) ? RequestBody.UrlEncoded(ParseFields(text)) : RequestBody.Raw(RawContentType.Text, text);
        }

        return RequestBody.Raw(ImportHelpers.ContentTypeFromMime(contentType), text);
    }

    private static bool ContainsAny(string text, params string[] parts) =>
        parts.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static bool IsFormShaped(string text, bool strict)
    {
        if (strict && (text.Contains('{') || text.Any(char.IsWhiteSpace)))
        {
            return false;
        }

        return text.Split('&').All(p => p.Contains('='));
    }

    private static List<FormField> ParseFields(string text) =>
        [.. text.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p =>
        {
            var (key, value) = SplitPair(p);
            return new FormField(key, value);
        })];

    private static (string Key, string Value) SplitPair(string piece)
    {
        var eq = piece.IndexOf('=', StringComparison.Ordinal);
        return eq < 0 ? (Decode(piece), "") : (Decode(piece[..eq]), Decode(piece[(eq + 1)..]));
    }

    private static string Decode(string text)
    {
        try
        {
            return Uri.UnescapeDataString(text.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return text;
        }
    }

    /// <summary>Reads the option or argument at <paramref name="index"/> and returns the index of the last word it used.</summary>
    private static int ReadWord(Command cmd, IReadOnlyList<string> words, int index)
    {
        var word = words[index];
        if (word.Length == 0 || word[0] != '-')
        {
            cmd.SetUrl(word);
            return index;
        }

        if (word.StartsWith("--", StringComparison.Ordinal))
        {
            var eq = word.IndexOf('=', StringComparison.Ordinal);
            var name = eq < 0 ? word[2..] : word[2..eq];
            string? inline = eq < 0 ? null : word[(eq + 1)..];
            return Apply(cmd, name, word, inline, words, index);
        }

        var letters = word[1..];
        if (letters.Length > 1 && letters.All(c => NoValueGroupLetters.Contains(c, StringComparison.Ordinal)))
        {
            foreach (var letter in letters)
            {
                Apply(cmd, ShortNames[letter.ToString()], "-" + letter, null, words, index);
            }

            return index;
        }

        if (letters.Length > 0 && ShortNames.TryGetValue(letters[..1], out var shortName) && ValueOptions.Contains(shortName))
        {
            // -XPOST style: the value is glued to the flag.
            return Apply(cmd, shortName, word[..2], letters.Length > 1 ? letters[1..] : null, words, index);
        }

        if (letters.Length == 1 && ShortNames.TryGetValue(letters, out shortName))
        {
            return Apply(cmd, shortName, word, null, words, index);
        }

        cmd.Warn(Strings.ImportCurlOptionIgnored(word));
        return index;
    }

    private static int Apply(Command cmd, string name, string shown, string? inline, IReadOnlyList<string> words, int index)
    {
        string? value = null;
        if (ValueOptions.Contains(name))
        {
            if (inline is not null)
            {
                value = inline;
            }
            else if (index + 1 < words.Count)
            {
                value = words[++index];
            }
            else
            {
                return index;
            }
        }

        switch (name)
        {
            case "request": cmd.MethodText = value!; break;
            case "header": cmd.AddHeader(value!); break;
            case "data" or "data-ascii":
                if (value!.StartsWith('@'))
                {
                    cmd.BinaryPath = value[1..];
                }
                else
                {
                    cmd.Data.Add(value);
                }

                break;
            case "data-raw": cmd.Data.Add(value!); break;
            case "data-binary":
                if (value!.StartsWith('@'))
                {
                    cmd.BinaryPath = value[1..];
                }
                else
                {
                    cmd.Data.Add(value);
                }

                break;
            case "data-urlencode": cmd.AddUrlEncoded(value!, shown); break;
            case "form": cmd.AddForm(value!, false, shown); break;
            case "form-string": cmd.AddForm(value!, true, shown); break;
            case "user":
                var colon = value!.IndexOf(':', StringComparison.Ordinal);
                cmd.User = colon < 0 ? value : value[..colon];
                cmd.Secret = colon < 0 ? "" : value[(colon + 1)..];
                break;
            case "digest": cmd.Digest = true; break;
            case "ntlm" or "negotiate": cmd.Warn(Strings.ImportCurlOptionIgnored(shown)); break;
            case "oauth2-bearer": cmd.Bearer = value; break;
            case "user-agent": cmd.SetHeader("User-Agent", value!); break;
            case "referer": cmd.Headers.Add(new KeyValueEntry("Referer", value!)); break;
            case "cookie":
                if (value!.Contains('='))
                {
                    cmd.Headers.Add(new KeyValueEntry("Cookie", value));
                }
                else
                {
                    cmd.Warn(Strings.ImportCurlOptionIgnored(shown));
                }

                break;
            case "head": cmd.Head = true; break;
            case "get": cmd.Get = true; break;
            case "max-time":
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    && seconds > 0 && Math.Ceiling(seconds) <= int.MaxValue)
                {
                    cmd.Timeout = (int)Math.Ceiling(seconds);
                }

                break;
            case "max-redirs":
                if (value!.Trim() == "0")
                {
                    cmd.Follow = false;
                }

                break;
            case "location": cmd.Follow = true; break;
            case "http1.0" or "http1.1": cmd.Version = HttpVersionPreference.Http1; break;
            case "http2" or "http2-prior-knowledge": cmd.Version = HttpVersionPreference.Http2; break;
            case "http3": cmd.Version = HttpVersionPreference.Http3; break;
            case "url": cmd.SetUrl(value!); break;
            case "insecure": cmd.Warn(Strings.ImportCurlInsecure); break;
            case "compressed" or "silent" or "show-error" or "verbose" or "include" or "fail" or "globoff" or "no-buffer"
                or "tr-encoding" or "path-as-is":
                break;
            default:
                cmd.Warn(Strings.ImportCurlOptionIgnored(shown));

                if (name == "upload-file")
                {
                    cmd.Upload = true;
                }

                break;
        }

        return index;
    }

    private sealed class Command
    {
        public string Url { get; private set; } = "";

        public string? MethodText { get; set; }

        public List<KeyValueEntry> Headers { get; } = [];

        public List<string> Data { get; } = [];

        public List<FormField> UrlEncoded { get; } = [];

        public List<FormField> Form { get; } = [];

        public string? BinaryPath { get; set; }

        public string? User { get; set; }

        public string Secret { get; set; } = "";

        public bool Digest { get; set; }

        public string? Bearer { get; set; }

        public bool Head { get; set; }

        public bool Get { get; set; }

        public bool Upload { get; set; }

        public bool Follow { get; set; } = true;

        public int Timeout { get; set; }

        public HttpVersionPreference Version { get; set; } = HttpVersionPreference.Default;

        public List<(ImportSeverity Severity, string Message)> Notes { get; } = [];

        public void Warn(string message) => Notes.Add((ImportSeverity.Warning, message));

        public void SetUrl(string url)
        {
            if (Url.Length == 0)
            {
                Url = url;
            }
            else
            {
                Warn(Strings.ImportCurlExtraArgument(url));
            }
        }

        public void SetHeader(string name, string value)
        {
            var at = Headers.FindIndex(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
            {
                Headers[at] = new KeyValueEntry(name, value);
            }
            else
            {
                Headers.Add(new KeyValueEntry(name, value));
            }
        }

        public void AddHeader(string text)
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                if (text.EndsWith(';') && text.TrimEnd(';').Trim() is { Length: > 0 } bare)
                {
                    Headers.Add(new KeyValueEntry(bare, ""));
                }
                else
                {
                    Warn(Strings.ImportCurlOptionIgnored("-H"));
                }

                return;
            }

            var name = text[..colon].Trim();
            var value = text[(colon + 1)..].Trim();
            if (name.Length == 0)
            {
                Warn(Strings.ImportCurlOptionIgnored("-H"));
            }
            else if (value.Length == 0)
            {
                Notes.Add((ImportSeverity.Info, Strings.ImportCurlOptionIgnored("-H \"" + name + ":\"")));
            }
            else
            {
                Headers.Add(new KeyValueEntry(name, value));
            }
        }

        public void AddUrlEncoded(string text, string shown)
        {
            var eq = text.IndexOf('=', StringComparison.Ordinal);
            if (eq >= 0)
            {
                UrlEncoded.Add(new FormField(text[..eq], text[(eq + 1)..]));
            }
            else if (text.Contains('@'))
            {
                Warn(Strings.ImportCurlOptionIgnored(shown + " " + text));
            }
            else
            {
                UrlEncoded.Add(new FormField("", text));
            }
        }

        public void AddForm(string text, bool alwaysText, string shown)
        {
            var eq = text.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
            {
                Warn(Strings.ImportCurlOptionIgnored(shown));
                return;
            }

            var key = text[..eq];
            var value = text[(eq + 1)..];
            if (!alwaysText && value.Length > 0 && value[0] is '@' or '<')
            {
                var path = value[1..];
                var type = path.IndexOf(";type=", StringComparison.Ordinal);
                Form.Add(new FormField(key, type < 0 ? path : path[..type], true, FormFieldKind.File));
            }
            else
            {
                Form.Add(new FormField(key, value));
            }
        }
    }
}
