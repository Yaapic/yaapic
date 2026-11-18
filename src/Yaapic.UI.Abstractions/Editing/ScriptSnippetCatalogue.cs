using System.Text.RegularExpressions;

namespace Yaapic.UI.Abstractions.Editing;

/// <summary>The script phase a snippet applies to, or the phase an editor shows.</summary>
public enum ScriptSnippetPhase
{
    /// <summary>Both phases: for a snippet, usable anywhere; for an editor, no filtering.</summary>
    Any,

    /// <summary>Pre-request scripts.</summary>
    PreRequest,

    /// <summary>Post-response scripts.</summary>
    PostResponse,
}

/// <summary>A ready-made script the user inserts from the script editor.</summary>
/// <param name="Prefix">The short text that offers the snippet in completion.</param>
/// <param name="Name">The name shown in the Snippets menu; English, like the API names.</param>
/// <param name="Phase">The phase it applies to.</param>
/// <param name="JavaScript">The JavaScript template in TextMate snippet syntax (<c>${1:default}</c>, <c>$0</c>); null when there is none.</param>
/// <param name="Lua">The Lua template in TextMate snippet syntax; null when there is none.</param>
public sealed partial record ScriptSnippet(string Prefix, string Name, ScriptSnippetPhase Phase, string? JavaScript, string? Lua)
{
    /// <summary>Gets the template for a language.</summary>
    /// <param name="lua"><see langword="true"/> for Lua.</param>
    /// <returns>The template, or null when the snippet does not exist in that language.</returns>
    public string? Template(bool lua) => lua ? Lua : JavaScript;

    /// <summary>Tells whether the snippet can be offered in an editor.</summary>
    /// <param name="lua"><see langword="true"/> for a Lua editor.</param>
    /// <param name="editorPhase">The phase the editor shows; <see cref="ScriptSnippetPhase.Any"/> keeps every snippet.</param>
    /// <returns><see langword="true"/> when the language and the phase match.</returns>
    public bool AppliesTo(bool lua, ScriptSnippetPhase editorPhase) =>
        Template(lua) is not null && (Phase == ScriptSnippetPhase.Any || editorPhase == ScriptSnippetPhase.Any || Phase == editorPhase);

    /// <summary>Gets the script a template yields when every tab stop keeps its default text.</summary>
    /// <param name="template">A template in TextMate snippet syntax.</param>
    /// <returns>The plain script.</returns>
    public static string PlainText(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return TabStop().Replace(template, m => m.Groups[1].Success ? m.Groups[1].Value : "");
    }

    [GeneratedRegex(@"\$(?:\d+|\{\d+(?::([^}]*))?\})")]
    private static partial Regex TabStop();
}

/// <summary>The built-in snippets of the script editors, in JavaScript and Lua; every one is run against the real engines by <c>ScriptSnippetCatalogueTests</c>.</summary>
public static class ScriptSnippetCatalogue
{
    private const ScriptSnippetPhase Pre = ScriptSnippetPhase.PreRequest;
    private const ScriptSnippetPhase Post = ScriptSnippetPhase.PostResponse;
    private const ScriptSnippetPhase Any = ScriptSnippetPhase.Any;

    /// <summary>Gets every built-in snippet.</summary>
    public static IReadOnlyList<ScriptSnippet> All { get; } =
    [
        new("setVar", "Set an environment variable", Any,
            """yaapic.environment.set("${1:name}", "${2:value}");$0""",
            """yaapic.environment.set("${1:name}", "${2:value}")$0"""),
        new("getVar", "Read a variable", Any,
            """const ${1:value} = yaapic.variables.get("${2:name}");$0""",
            """local ${1:value} = yaapic.variables.get("${2:name}")$0"""),
        new("log", "Log to the console", Any,
            """console.log(${1:"message"});$0""",
            """console.log(${1:"message"})$0"""),
        new("replaceVars", "Resolve {{variables}} in a text", Any,
            """const ${1:text} = yaapic.variables.replaceIn("{{${2:baseUrl}}}/path");$0""",
            """local ${1:text} = yaapic.variables.replace_in("{{${2:baseUrl}}}/path")$0"""),
        new("setGlobal", "Set a global variable", Any,
            """yaapic.globals.set("${1:name}", "${2:value}");$0""",
            """yaapic.globals.set("${1:name}", "${2:value}")$0"""),
        new("preHeader", "Add or replace a request header", Pre,
            """yaapic.request.headers.upsert({ key: "${1:X-Request-Id}", value: "${2:value}" });$0""",
            """yaapic.request.headers.upsert({ key = "${1:X-Request-Id}", value = "${2:value}" })$0"""),
        new("preBearer", "Send a bearer token from a variable", Pre,
            """yaapic.request.headers.upsert({ key: "Authorization", value: "Bearer " + yaapic.variables.get("${1:token}") });$0""",
            """yaapic.request.headers.upsert({ key = "Authorization", value = "Bearer " .. tostring(yaapic.variables.get("${1:token}")) })$0"""),
        new("preQuery", "Add or replace a query parameter", Pre,
            """yaapic.request.url.query.upsert({ key: "${1:page}", value: "${2:1}" });$0""",
            """yaapic.request.url.query.upsert({ key = "${1:page}", value = "${2:1}" })$0"""),
        new("preRefreshToken", "Refresh the token when it is missing", Pre,
            """
            if (!yaapic.environment.get("token")) {
                yaapic.sendRequest("${1:https://api.example.com/token}", (err, res) => {
                    if (!err) {
                        yaapic.environment.set("token", res.json().${2:token});
                    }
                });
            }$0
            """,
            """
            if not yaapic.environment.get("token") then
                local res, err = yaapic.send_request("${1:https://api.example.com/token}")
                if not err then
                    yaapic.environment.set("token", res.json().${2:token})
                end
            end$0
            """),
        new("preBody", "Replace the raw request body", Pre,
            """yaapic.request.body.raw = JSON.stringify({ ${1:id}: ${2:1} });$0""",
            null),
        new("testStatus", "Test the status code", Post,
            """
            yaapic.test("Status is ${1:200}", () => {
                yaapic.response.to.have.status(${1:200});
            });$0
            """,
            """
            yaapic.test("Status is ${1:200}", function()
                yaapic.response.to.have.status(${1:200})
            end)$0
            """),
        new("testOk", "Test that the request succeeded", Post,
            """
            yaapic.test("Response is successful", () => {
                yaapic.response.to.be.success;
            });$0
            """,
            """
            yaapic.test("Response is successful", function()
                yaapic.response.to.be.success()
            end)$0
            """),
        new("testTime", "Test the response time", Post,
            """
            yaapic.test("Response time is below ${1:500} ms", () => {
                yaapic.expect(yaapic.response.responseTime).to.be.below(${1:500});
            });$0
            """,
            """
            yaapic.test("Response time is below ${1:500} ms", function()
                yaapic.expect(yaapic.response.response_time).to.be.below(${1:500})
            end)$0
            """),
        new("testJson", "Test a JSON field", Post,
            """
            yaapic.test("${1:count} is ${2:2}", () => {
                const json = yaapic.response.json();
                yaapic.expect(json.${1:count}).to.equal(${2:2});
            });$0
            """,
            """
            yaapic.test("${1:count} is ${2:2}", function()
                local json = yaapic.response.json()
                yaapic.expect(json.${1:count}).to.equal(${2:2})
            end)$0
            """),
        new("testHeader", "Test that a header exists", Post,
            """
            yaapic.test("Header ${1:Content-Type} is present", () => {
                yaapic.response.to.have.header("${1:Content-Type}");
            });$0
            """,
            """
            yaapic.test("Header ${1:Content-Type} is present", function()
                yaapic.response.to.have.header("${1:Content-Type}")
            end)$0
            """),
        new("testBody", "Test that the body contains a text", Post,
            """
            yaapic.test("Body contains ${1:token}", () => {
                yaapic.expect(yaapic.response.text()).to.include("${1:token}");
            });$0
            """,
            """
            yaapic.test("Body contains ${1:token}", function()
                yaapic.expect(yaapic.response.text()).to.include("${1:token}")
            end)$0
            """),
        new("saveJson", "Save a JSON field to a variable", Post,
            """yaapic.environment.set("${1:token}", yaapic.response.json().${2:token});$0""",
            """yaapic.environment.set("${1:token}", yaapic.response.json().${2:token})$0"""),
        new("saveHeader", "Save a response header to a variable", Post,
            """yaapic.environment.set("${1:etag}", yaapic.response.headers.get("${2:ETag}"));$0""",
            """yaapic.environment.set("${1:etag}", yaapic.response.headers.get("${2:ETag}"))$0"""),
        new("saveCookie", "Save a response cookie to a variable", Post,
            """yaapic.environment.set("${1:session}", yaapic.cookies.get("${1:session}") ?? "");$0""",
            """yaapic.environment.set("${1:session}", yaapic.cookies.get("${1:session}") or "")$0"""),
        new("sendRequest", "Chain another request", Post,
            """
            yaapic.sendRequest("${1:https://api.example.com/next}", (err, res) => {
                if (!err) {
                    yaapic.environment.set("${2:next}", String(res.code));
                }
            });$0
            """,
            """
            local res, err = yaapic.send_request("${1:https://api.example.com/next}")
            if not err then
                yaapic.environment.set("${2:next}", tostring(res.code))
            end$0
            """),
        new("eachItem", "Loop over a JSON array", Post,
            """
            for (const item of yaapic.response.json().${1:data}) {
                console.log(item.${2:id});
            }$0
            """,
            """
            for _, item in ipairs(yaapic.response.json().${1:data}) do
                console.log(item.${2:id})
            end$0
            """),
    ];

    /// <summary>Lists the snippets an editor offers.</summary>
    /// <param name="lua"><see langword="true"/> for a Lua editor.</param>
    /// <param name="editorPhase">The phase the editor shows.</param>
    /// <returns>The matching snippets, in catalogue order.</returns>
    public static IReadOnlyList<ScriptSnippet> For(bool lua, ScriptSnippetPhase editorPhase) =>
        [.. All.Where(s => s.AppliesTo(lua, editorPhase))];
}
