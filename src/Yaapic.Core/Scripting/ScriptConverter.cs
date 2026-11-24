using System.Text;
using System.Text.RegularExpressions;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Scripting;

/// <summary>The scripting API a pasted script was written for.</summary>
public enum ScriptDialect
{
    /// <summary>Postman: <c>pm.*</c> and the legacy <c>postman.*</c> calls.</summary>
    Postman,

    /// <summary>Bruno: <c>bru.*</c>, <c>req.*</c>, <c>res.*</c>, and bare <c>test</c>/<c>expect</c>.</summary>
    Bruno,
}

/// <summary>Something a conversion could not translate, or translated with a caveat.</summary>
/// <param name="Line">The 1-based line in the pasted source.</param>
/// <param name="Member">The member as written in the source, for example <c>pm.execution.skipRequest</c>.</param>
/// <param name="Reason">Why it needs attention.</param>
public sealed record ScriptConversionFinding(int Line, string Member, string Reason);

/// <summary>The outcome of <see cref="ScriptConverter.Convert"/>.</summary>
/// <param name="Text">The converted script.</param>
/// <param name="Findings">What could not be converted, in source order.</param>
public sealed record ScriptConversionResult(string Text, IReadOnlyList<ScriptConversionFinding> Findings);

/// <summary>
/// Rewrites a Postman or Bruno script (JavaScript) to the <c>yaapic</c> API. It scans the source token by token, skipping strings,
/// comments and template-literal text, and rewrites member chains rooted at the dialect's globals; members with no equivalent
/// are reported (Postman ones are renamed to <c>yaapic</c> anyway, so they fail at run time with Yaapic's own message).
/// </summary>
public static partial class ScriptConverter
{
    private sealed record Mapping(string? To, string? Note = null, bool DropCall = false, bool Assign = false);

    private static readonly string[] PostmanUnsupported =
    [
        "pm.execution.location", "pm.execution.runRequest", "pm.vault", "pm.visualizer", "pm.require", "pm.request.auth", "pm.request.certificate",
        "pm.environment.name", "pm.cookies.jar", "pm.response.to.have.jsonSchema",
    ];

    private static readonly Dictionary<string, string> PostmanLegacy = new(StringComparer.Ordinal)
    {
        ["postman.setNextRequest"] = "yaapic.execution.setNextRequest",
        ["postman.setEnvironmentVariable"] = "yaapic.environment.set",
        ["postman.getEnvironmentVariable"] = "yaapic.environment.get",
        ["postman.clearEnvironmentVariable"] = "yaapic.environment.unset",
        ["postman.setGlobalVariable"] = "yaapic.globals.set",
        ["postman.getGlobalVariable"] = "yaapic.globals.get",
        ["postman.clearGlobalVariable"] = "yaapic.globals.unset",
    };

    private static readonly Dictionary<string, Mapping> Bruno = new(StringComparer.Ordinal)
    {
        ["bru.getEnvVar"] = new("yaapic.environment.get"),
        ["bru.setEnvVar"] = new("yaapic.environment.set"),
        ["bru.getVar"] = new("yaapic.variables.get"),
        ["bru.setVar"] = new("yaapic.variables.set"),
        ["bru.hasVar"] = new("yaapic.variables.has"),
        ["bru.deleteVar"] = new("yaapic.variables.unset"),
        ["bru.getGlobalEnvVar"] = new("yaapic.globals.get"),
        ["bru.setGlobalEnvVar"] = new("yaapic.globals.set"),
        ["bru.getCollectionVar"] = new("yaapic.collectionVariables.get"),
        ["bru.interpolate"] = new("yaapic.variables.replaceIn"),
        ["bru.setNextRequest"] = new("yaapic.execution.setNextRequest"),
        ["bru.runner.skipRequest"] = new("yaapic.execution.skipRequest"),
        ["bru.runner.stopExecution"] = new(null, "use yaapic.execution.setNextRequest(null)"),
        ["bru.getEnvName"] = new(null, "yaapic.environment.name is not supported in Yaapic"),
        ["req.getHeader"] = new("yaapic.request.headers.get"),
        ["req.setHeader"] = new("yaapic.request.headers.upsert"),
        ["req.deleteHeader"] = new("yaapic.request.headers.remove"),
        ["req.getMethod"] = new("yaapic.request.method", DropCall: true),
        ["req.setMethod"] = new("yaapic.request.method", Assign: true),
        ["req.getUrl"] = new("yaapic.request.url.toString"),
        ["req.setUrl"] = new("yaapic.request.url.update"),
        ["req.getBody"] = new("yaapic.request.body.raw", DropCall: true),
        ["req.setBody"] = new("yaapic.request.body.raw", "converted to the raw body, which must be a string (use JSON.stringify for an object)", Assign: true),
        ["req.getName"] = new("yaapic.info.requestName", DropCall: true),
        ["res.status"] = new("yaapic.response.code"),
        ["res.getStatus"] = new("yaapic.response.code", DropCall: true),
        ["res.body"] = new("yaapic.response.json()"),
        ["res.getBody"] = new("yaapic.response.json"),
        ["res.headers"] = new("yaapic.response.headers"),
        ["res.getHeader"] = new("yaapic.response.headers.get"),
        ["res.getHeaders"] = new("yaapic.response.headers", DropCall: true),
        ["res.responseTime"] = new("yaapic.response.responseTime"),
        ["res.getResponseTime"] = new("yaapic.response.responseTime", DropCall: true),
    };

    // Libraries Postman and Bruno bundle that Yaapic does not provide (docs/scripting-compatibility.md).
    private static readonly HashSet<string> MissingLibraries = new(StringComparer.Ordinal)
    {
        "require", "CryptoJS", "moment", "cheerio", "tv4", "lodash", "atob", "btoa",
    };

    /// <summary>Guesses the dialect of a script: Bruno when it uses <c>bru</c>/<c>req</c>/<c>res</c> and no Postman global, else Postman.</summary>
    /// <param name="source">The pasted script.</param>
    /// <returns>The detected dialect.</returns>
    public static ScriptDialect Detect(string source) =>
        !PostmanRoot().IsMatch(source) && BrunoRoot().IsMatch(source) ? ScriptDialect.Bruno : ScriptDialect.Postman;

    /// <summary>Converts a script to the <c>yaapic</c> API.</summary>
    /// <param name="source">The pasted script.</param>
    /// <param name="dialect">The API it was written for.</param>
    /// <param name="language">The language of the target editor; only JavaScript scripts can be converted.</param>
    /// <returns>The converted text and the findings.</returns>
    public static ScriptConversionResult Convert(string source, ScriptDialect dialect, ScriptLanguage language)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (language != ScriptLanguage.JavaScript)
        {
            return new(source, [new(1, "", Strings.ScriptConvertNotJs)]);
        }

        return new Converter(source, dialect).Run();
    }

    [GeneratedRegex(@"(?<![\w$.])(pm|postman)\.")]
    private static partial Regex PostmanRoot();

    [GeneratedRegex(@"(?<![\w$.])(bru|req|res)\.")]
    private static partial Regex BrunoRoot();

    private sealed class Converter(string src, ScriptDialect dialect)
    {
        private readonly StringBuilder _out = new(src.Length + 64);
        private readonly List<ScriptConversionFinding> _findings = [];
        private readonly List<int> _lineStarts = LineStarts(src);
        private int _pos;

        public ScriptConversionResult Run()
        {
            Scan(inTemplate: false);
            return new(_out.ToString(), _findings);
        }

        private static List<int> LineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return starts;
        }

        private int LineOf(int pos)
        {
            var index = _lineStarts.BinarySearch(pos);
            return (index >= 0 ? index : ~index - 1) + 1;
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '$';

        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

        private bool At(char c, int offset = 1) => _pos + offset < src.Length && src[_pos + offset] == c;

        // shortcut: regular-expression literals are not recognised, so a quote inside one can hide the rest of the line; upgrade with a regex-aware tokenizer if reports show it.
        private void Scan(bool inTemplate)
        {
            var depth = 0;
            while (_pos < src.Length)
            {
                var c = src[_pos];
                if (c == '/' && At('/'))
                {
                    CopyUntil("\n", includeEnd: false);
                }
                else if (c == '/' && At('*'))
                {
                    CopyUntil("*/", includeEnd: true);
                }
                else if (c is '\'' or '"')
                {
                    CopyString(c);
                }
                else if (c == '`')
                {
                    CopyTemplate();
                }
                else if (c == '}' && inTemplate && depth == 0)
                {
                    return;
                }
                else if (IsIdentStart(c) && (_pos == 0 || (!IsIdentPart(src[_pos - 1]) && src[_pos - 1] != '.')))
                {
                    Identifier();
                }
                else
                {
                    depth += c == '{' ? 1 : c == '}' ? -1 : 0;
                    _out.Append(c);
                    _pos++;
                }
            }
        }

        private void CopyUntil(string end, bool includeEnd)
        {
            var at = src.IndexOf(end, _pos + (includeEnd ? 2 : 0), StringComparison.Ordinal);
            var stop = at < 0 ? src.Length : includeEnd ? at + end.Length : at;
            _out.Append(src, _pos, stop - _pos);
            _pos = stop;
        }

        private void CopyString(char quote)
        {
            _out.Append(quote);
            _pos++;
            while (_pos < src.Length)
            {
                var c = src[_pos];
                _out.Append(c);
                _pos++;
                if (c == '\\' && _pos < src.Length)
                {
                    _out.Append(src[_pos++]);
                }
                else if (c == quote || c == '\n')
                {
                    return;
                }
            }
        }

        private void CopyTemplate()
        {
            _out.Append('`');
            _pos++;
            while (_pos < src.Length)
            {
                var c = src[_pos];
                if (c == '\\' && _pos + 1 < src.Length)
                {
                    _out.Append(c).Append(src[_pos + 1]);
                    _pos += 2;
                }
                else if (c == '`')
                {
                    _out.Append(c);
                    _pos++;
                    return;
                }
                else if (c == '$' && At('{'))
                {
                    _out.Append("${");
                    _pos += 2;
                    Scan(inTemplate: true);
                    if (_pos < src.Length)
                    {
                        _out.Append('}');
                        _pos++;
                    }
                }
                else
                {
                    _out.Append(c);
                    _pos++;
                }
            }
        }

        private void Identifier()
        {
            var start = _pos;
            var segments = new List<string>();
            while (true)
            {
                var s = _pos;
                while (_pos < src.Length && IsIdentPart(src[_pos]))
                {
                    _pos++;
                }

                segments.Add(src[s.._pos]);
                if (_pos + 1 < src.Length && src[_pos] == '.' && IsIdentStart(src[_pos + 1]))
                {
                    _pos++;
                    continue;
                }

                break;
            }

            var line = LineOf(start);
            var chain = string.Join('.', segments);
            var root = segments[0];
            var consumed = chain.Length; // chain characters replaced by `text`
            string? text = null;

            if (dialect == ScriptDialect.Postman && root == "pm")
            {
                var bad = Array.Find(PostmanUnsupported, p => chain == p || chain.StartsWith(p + ".", StringComparison.Ordinal));
                if (bad is not null)
                {
                    _findings.Add(new(line, chain, $"{bad.Replace("pm.", "yaapic.", StringComparison.Ordinal)} is not supported in Yaapic"));
                }

                text = "yaapic";
                consumed = 2;
            }
            else if (dialect == ScriptDialect.Postman && root == "postman" && segments.Count > 1)
            {
                var key = $"postman.{segments[1]}";
                if (PostmanLegacy.TryGetValue(key, out var to))
                {
                    text = to;
                    consumed = key.Length;
                }
                else
                {
                    _findings.Add(new(line, key, "no yaapic equivalent"));
                }
            }
            else if (dialect == ScriptDialect.Bruno && root is "bru" or "req" or "res")
            {
                (text, consumed) = RewriteBruno(segments, chain, line);
            }
            else if (dialect == ScriptDialect.Bruno && segments.Count == 1 && root is "test" or "expect" && NextIsCall())
            {
                text = $"yaapic.{root}";
            }
            else if (MissingLibraries.Contains(root))
            {
                _findings.Add(new(line, chain, $"{root} is not provided in Yaapic"));
            }

            if (text is null)
            {
                _out.Append(src, start, _pos - start);
                return;
            }

            _out.Append(text).Append(chain, consumed, chain.Length - consumed);
            if (consumed == chain.Length && DropsCall(chain))
            {
                SkipEmptyCall();
            }
        }

        private (string? Text, int Consumed) RewriteBruno(List<string> segments, string chain, int line)
        {
            for (var n = segments.Count; n >= 2; n--)
            {
                var key = string.Join('.', segments.Take(n));
                if (!Bruno.TryGetValue(key, out var map))
                {
                    continue;
                }

                if (map.To is null)
                {
                    _findings.Add(new(line, key, map.Note!));
                    return (null, 0);
                }

                if (map.Note is not null)
                {
                    _findings.Add(new(line, key, map.Note));
                }

                // An assignment target replaces the call parentheses' name only: `req.setUrl(x)` style stays valid as `target = (x)`.
                return (map.Assign ? map.To + " =" : map.To, key.Length);
            }

            _findings.Add(new(line, chain, "no yaapic equivalent"));
            return (null, 0);
        }

        private bool DropsCall(string chain) =>
            dialect == ScriptDialect.Bruno && Bruno.TryGetValue(chain, out var map) && map.DropCall;

        private bool NextIsCall()
        {
            var i = _pos;
            while (i < src.Length && char.IsWhiteSpace(src[i]))
            {
                i++;
            }

            return i < src.Length && src[i] == '(';
        }

        // `req.getMethod()` becomes the property `yaapic.request.method`: remove the empty parentheses that follow.
        private void SkipEmptyCall()
        {
            var i = _pos;
            if (i >= src.Length || src[i] != '(')
            {
                return;
            }

            i++;
            while (i < src.Length && char.IsWhiteSpace(src[i]))
            {
                i++;
            }

            if (i < src.Length && src[i] == ')')
            {
                _pos = i + 1;
            }
        }
    }
}
