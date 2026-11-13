namespace Yaapic.UI.Abstractions.Editing;

/// <summary>How a member of the script API is used.</summary>
public enum ScriptApiKind
{
    /// <summary>A value or an object holding more members.</summary>
    Property,

    /// <summary>A function.</summary>
    Method,
}

/// <summary>One member of the script API offered by completion.</summary>
/// <param name="Name">The name in JavaScript.</param>
/// <param name="Description">A one-line description; English, like the API names.</param>
/// <param name="Kind">Whether it is called.</param>
/// <param name="Signature">The parameters shown after a method name, such as <c>(name, fn)</c>.</param>
/// <param name="LuaName">The name in Lua when it differs from <paramref name="Name"/>.</param>
/// <param name="Members">The members reached through this one; null for none.</param>
/// <param name="IsChain">Whether this member continues into the assertion chain (<c>to</c>, <c>be</c>, <c>equal</c>...).</param>
public sealed record ScriptApiMember(
    string Name,
    string Description,
    ScriptApiKind Kind = ScriptApiKind.Property,
    string Signature = "",
    string? LuaName = null,
    IReadOnlyList<ScriptApiMember>? Members = null,
    bool IsChain = false)
{
    /// <summary>Gets the name to type in a language.</summary>
    /// <param name="lua"><see langword="true"/> for Lua.</param>
    /// <returns>The Lua name when there is one, else the JavaScript name.</returns>
    public string NameIn(bool lua) => lua ? LuaName ?? Name : Name;

    /// <summary>Tells whether the member exists in a language; <c>setMethod</c> and <c>setUrl</c> are Lua only (JavaScript assigns the properties).</summary>
    /// <param name="lua"><see langword="true"/> for Lua.</param>
    /// <returns><see langword="true"/> when the language has the member.</returns>
    public bool ExistsIn(bool lua) => lua || Name is not ("setMethod" or "setUrl");
}

/// <summary>The members of the <c>yaapic</c> API (also reachable as <c>pm</c>) and of <c>console</c> that script editors complete, for JavaScript and Lua.</summary>
/// <remarks>
/// Kept in step with the engines by <c>ScriptApiCatalogueTests</c>, which checks every entry exists on the real engine.
/// A path such as <c>pm.response.</c> or <c>pm.expect(x).to.</c> is resolved by <see cref="Complete"/>.
/// </remarks>
public static class ScriptApiCatalogue
{
    private static ScriptApiMember P(string name, string description, string? lua = null, IReadOnlyList<ScriptApiMember>? members = null, bool chain = false) =>
        new(name, description, ScriptApiKind.Property, "", lua, members, chain);

    private static ScriptApiMember F(string name, string description, string signature, string? lua = null, IReadOnlyList<ScriptApiMember>? members = null, bool chain = false) =>
        new(name, description, ScriptApiKind.Method, signature, lua, members, chain);

    private static readonly ScriptApiMember[] Lists =
    [
        F("get", "Gets a value by name.", "(name)"),
        F("has", "Tells whether a name exists.", "(name)"),
        F("add", "Adds an entry.", "({ key, value })"),
        F("upsert", "Adds an entry or replaces the one of the same name.", "({ key, value })"),
        F("remove", "Removes an entry by name.", "(name)"),
        F("each", "Calls a function for every entry.", "(fn)"),
        F("count", "Counts the entries.", "()"),
        F("all", "Gets every entry.", "()"),
        F("toObject", "Converts the entries to an object.", "()", "to_object"),
    ];

    // Response headers and cookies cannot be changed.
    private static readonly ScriptApiMember[] ReadOnlyLists = [.. Lists.Where(m => m.Name is not ("add" or "upsert" or "remove"))];

    private static readonly ScriptApiMember[] VariableScope =
    [
        F("get", "Gets a variable.", "(name)"),
        F("set", "Sets a variable.", "(name, value)"),
        F("unset", "Removes a variable.", "(name)"),
        F("has", "Tells whether a variable exists.", "(name)"),
        F("clear", "Removes every variable of this scope.", "()"),
        F("toObject", "Converts the variables to an object.", "()", "to_object"),
    ];

    private static readonly ScriptApiMember[] Assertions =
    [
        F("equal", "Asserts strict equality.", "(value)"),
        F("eql", "Asserts deep equality.", "(value)"),
        F("above", "Asserts the value is greater than a number.", "(n)"),
        F("below", "Asserts the value is lower than a number.", "(n)"),
        F("least", "Asserts the value is at least a number.", "(n)"),
        F("most", "Asserts the value is at most a number.", "(n)"),
        F("within", "Asserts the value is within a range.", "(min, max)"),
        F("a", "Asserts the type of the value.", "(type)"),
        F("an", "Asserts the type of the value.", "(type)"),
        F("include", "Asserts a string, array or object contains a value.", "(value)"),
        F("contain", "Asserts a string, array or object contains a value.", "(value)"),
        F("property", "Asserts a property exists, optionally with a value.", "(name, value)"),
        F("lengthOf", "Asserts the length.", "(n)", "length_of"),
        F("match", "Asserts a string matches a regular expression (a pattern in Lua).", "(pattern)"),
        F("oneOf", "Asserts the value is one of a list.", "(list)", "one_of"),
        F("keys", "Asserts an object has the given keys.", "(...keys)"),
        F("members", "Asserts an array has the given members.", "(list)"),
        F("string", "Asserts a string contains a substring.", "(text)"),
        F("satisfy", "Asserts a predicate returns true.", "(fn)"),
        F("closeTo", "Asserts a number is close to another.", "(n, delta)", "close_to"),
        F("ok", "Asserts the value is truthy.", "()"),
        F("true", "Asserts the value is true.", "()", "is_true"),
        F("false", "Asserts the value is false.", "()", "is_false"),
        F("null", "Asserts the value is null.", "()", "is_null"),
        F("undefined", "Asserts the value is undefined.", "()", "is_nil"),
        F("exist", "Asserts the value is neither null nor undefined.", "()"),
        F("empty", "Asserts the value is empty.", "()"),
    ];

    private static readonly ScriptApiMember[] Chain =
    [
        P("to", "Chain word.", chain: true),
        P("be", "Chain word.", chain: true),
        P("been", "Chain word.", chain: true),
        P("is", "Chain word.", chain: true),
        P("that", "Chain word.", chain: true),
        P("which", "Chain word.", chain: true),
        P("has", "Chain word.", chain: true),
        P("have", "Chain word.", chain: true),
        P("with", "Chain word.", chain: true),
        P("at", "Chain word.", chain: true),
        P("of", "Chain word.", chain: true),
        P("same", "Chain word.", chain: true),
        P("but", "Chain word.", chain: true),
        P("does", "Chain word.", chain: true),
        P("and", "Chain word that joins two assertions.", "also", chain: true),
        P("not", "Negates the next assertion.", "is_not", chain: true),
        P("deep", "Compares deeply in the next assertion.", chain: true),
        P("nested", "Reads nested properties in the next assertion.", chain: true),
        P("own", "Only looks at own properties in the next assertion.", chain: true),
        P("ordered", "Compares members in order in the next assertion.", chain: true),
        P("any", "Matches any of the keys.", chain: true),
        P("all", "Matches all of the keys.", chain: true),
        .. Assertions,
    ];

    private static readonly ScriptApiMember[] ResponseTo =
    [
        P("be", "Response flags.", members: [
            P("ok", "Status is 200."),
            P("success", "Status is 2xx."),
            P("clientError", "Status is 4xx.", "client_error"),
            P("serverError", "Status is 5xx.", "server_error"),
            P("withBody", "The response has a body.", "with_body"),
            P("json", "The body is JSON."),
        ]),
        P("have", "Response checks.", members: [
            F("status", "Asserts the status code or reason.", "(codeOrText)"),
            F("header", "Asserts a header exists, optionally with a value.", "(name, value)"),
            F("body", "Asserts the body text.", "(text)"),
            F("jsonBody", "Asserts a JSON path, optionally with a value.", "(path, value)", "json_body"),
        ]),
    ];

    private static readonly ScriptApiMember[] Pm =
    [
        F("test", "Defines a test; a failed assertion is a test result, not a script error.", "(name, fn)"),
        F("expect", "Starts an assertion on a value.", "(value)", members: Chain, chain: true),
        P("response", "The response; post-response scripts only.", members:
        [
            P("code", "The status code."),
            P("status", "The status text."),
            P("responseTime", "The response time in milliseconds.", "response_time"),
            F("text", "The body as text.", "()"),
            F("json", "The body parsed as JSON.", "()"),
            P("headers", "The response headers.", members: ReadOnlyLists),
            P("to", "Assertions on the response.", members: ResponseTo),
            P("cookies", "The cookies set by the response.", members: ReadOnlyLists),
        ]),
        P("request", "The request being sent; editable in pre-request scripts.", members:
        [
            P("method", "The HTTP method."),
            P("url", "The URL.", members:
            [
                F("toString", "The URL as text.", "()", "to_string"),
                F("update", "Replaces the URL.", "(url)"),
                P("query", "The query parameters.", members: Lists),
            ]),
            P("headers", "The request headers.", members: Lists),
            P("body", "The request body.", members:
            [
                P("mode", "The body mode."),
                P("raw", "The raw body text."),
            ]),
            F("setMethod", "Sets the HTTP method (Lua).", "(method)", "set_method"),
            F("setUrl", "Sets the URL (Lua).", "(url)", "set_url"),
        ]),
        P("variables", "Variables resolved for this send.", members:
        [
            .. VariableScope,
            F("replaceIn", "Resolves the {{variables}} of a string.", "(text)", "replace_in"),
        ]),
        P("environment", "Variables of the active environment.", members: VariableScope),
        P("collectionVariables", "Variables of the collection.", "collection_variables", VariableScope),
        P("globals", "Global variables.", members: VariableScope),
        P("info", "Information about the running script.", members:
        [
            P("eventName", "prerequest or test."),
            P("requestName", "The name of the request."),
            P("requestId", "The identifier of the request."),
        ]),
        F("sendRequest", "Sends another request (a callback in JavaScript, two results in Lua).", "(url, callback)", "send_request"),
        P("cookies", "Read-only view of the Set-Cookie headers of the response.", members:
        [
            F("get", "Gets a cookie value.", "(name)"),
            F("has", "Tells whether a cookie exists.", "(name)"),
            F("toObject", "Converts the cookies to an object.", "()", "to_object"),
        ]),
    ];

    private static readonly ScriptApiMember[] Console =
    [
        F("log", "Writes to the console.", "(...values)"),
        F("info", "Writes an info line to the console.", "(...values)"),
        F("warn", "Writes a warning to the console.", "(...values)"),
        F("error", "Writes an error to the console.", "(...values)"),
        F("debug", "Writes a debug line to the console.", "(...values)"),
    ];

    private static readonly ScriptApiMember[] Roots =
    [
        P("yaapic", "The Yaapic script API.", members: Pm),
        P("pm", "Alias of yaapic, for Postman scripts.", members: Pm),
        P("console", "The console panel.", members: Console),
    ];

    /// <summary>Gets the members of the assertion chain, for the drift test.</summary>
    public static IReadOnlyList<ScriptApiMember> ChainMembers => Chain;

    /// <summary>Gets the top-level names (<c>yaapic</c>, <c>pm</c>, <c>console</c>).</summary>
    public static IReadOnlyList<ScriptApiMember> TopLevel => Roots;

    /// <summary>Lists what can be typed after the end of <paramref name="textBeforeCaret"/>.</summary>
    /// <param name="textBeforeCaret">The text of the script up to the caret; only the last line matters.</param>
    /// <param name="lua"><see langword="true"/> to complete Lua names.</param>
    /// <param name="explicitRequest"><see langword="true"/> for Ctrl+Space, which also lists the top-level names after nothing.</param>
    /// <returns>The prefix already typed and the matching members, prefix matches first; empty when nothing applies.</returns>
    public static (string Prefix, IReadOnlyList<ScriptApiMember> Members) Complete(string textBeforeCaret, bool lua, bool explicitRequest = false)
    {
        ArgumentNullException.ThrowIfNull(textBeforeCaret);
        var line = textBeforeCaret[(textBeforeCaret.LastIndexOf('\n') + 1)..];
        var end = line.Length;
        var start = end;
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        var prefix = line[start..end];
        IReadOnlyList<ScriptApiMember> candidates;
        if (start > 0 && line[start - 1] == '.')
        {
            var segments = Segments(line[..(start - 1)]);
            candidates = Resolve(segments);
        }
        else if (prefix.Length > 0 || explicitRequest)
        {
            candidates = Roots;
        }
        else
        {
            return ("", []);
        }

        var members = candidates
            .Where(m => m.NameIn(lua).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && m.ExistsIn(lua))
            .OrderBy(m => m.NameIn(lua), StringComparer.Ordinal)
            .ToList();
        return (prefix, members);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    // Splits the dotted expression that ends the text, ignoring the arguments of calls: "pm.expect(x).to" is [pm, expect, to].
    private static List<string> Segments(string text)
    {
        List<string> segments = [];
        var i = text.Length;
        while (i > 0)
        {
            while (i > 0 && text[i - 1] == ')')
            {
                var depth = 0;
                while (i > 0)
                {
                    var c = text[--i];
                    if (c == ')')
                    {
                        depth++;
                    }
                    else if (c == '(' && --depth == 0)
                    {
                        break;
                    }
                }
            }

            var end = i;
            while (i > 0 && IsWordChar(text[i - 1]))
            {
                i--;
            }

            if (end == i)
            {
                return [];
            }

            segments.Insert(0, text[i..end]);
            if (i > 0 && text[i - 1] == '.')
            {
                i--;
                continue;
            }

            break;
        }

        return segments;
    }

    private static IReadOnlyList<ScriptApiMember> Resolve(List<string> segments)
    {
        IReadOnlyList<ScriptApiMember> level = Roots;
        var inChain = false;
        foreach (var segment in segments)
        {
            var node = level.FirstOrDefault(m => m.Name == segment || m.LuaName == segment);
            if (node is null)
            {
                return [];
            }

            // Every member of an assertion returns the chain, so once in it the list stays the chain.
            inChain |= node.IsChain;
            level = inChain ? Chain : node.Members ?? [];
        }

        return level;
    }
}
