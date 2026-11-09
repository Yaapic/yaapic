using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Scripting;

/// <summary>
/// Turns the JSON calls of <c>__host(op, argsJson)</c> into calls on <see cref="IScriptHost"/>. The result is
/// <c>{"ok":&lt;json&gt;}</c> or <c>{"err":"message"}</c>; <see cref="Invoke"/> never throws. <c>req.set</c> takes the whole
/// request: the prelude keeps a local copy of <c>pm.request</c> and flushes its mutations in one call.
/// </summary>
/// <param name="host">The application side of the <c>pm</c> API.</param>
/// <param name="collector">Receives console lines and test results.</param>
internal sealed class HostBridge(IScriptHost host, RunCollector collector)
{
    /// <summary>Runs one operation.</summary>
    /// <param name="op">One of the <see cref="HostOps"/> constants.</param>
    /// <param name="argsJson">The JSON arguments of the operation.</param>
    /// <returns>The JSON result envelope.</returns>
    public string Invoke(string op, string argsJson)
    {
        try
        {
            return $"{{\"ok\":{Dispatch(op, argsJson)}}}";
        }
        catch (Exception ex)
        {
            // The bridge is called from native code and must never throw: every failure becomes an err envelope.
            return $"{{\"err\":{JsonSerializer.Serialize(ex.Message, HostJsonContext.Default.String)}}}";
        }
    }

    private string Dispatch(string op, string argsJson)
    {
        switch (op)
        {
            case HostOps.VarGet:
            {
                var a = Args(argsJson, HostJsonContext.Default.VarArgs);
                return host.TryGetVariable(Scope(a), Name(a), out var v) ? Json(v, HostJsonContext.Default.String) : "null";
            }

            case HostOps.VarHas:
            {
                var a = Args(argsJson, HostJsonContext.Default.VarArgs);
                return Json(host.TryGetVariable(Scope(a), Name(a), out _), HostJsonContext.Default.Boolean);
            }

            case HostOps.VarSet:
            {
                var a = Args(argsJson, HostJsonContext.Default.VarArgs);
                host.SetVariable(Scope(a), Name(a), a.Value ?? "");
                return "null";
            }

            case HostOps.VarUnset:
            {
                var a = Args(argsJson, HostJsonContext.Default.VarArgs);
                return Json(host.UnsetVariable(Scope(a), Name(a)), HostJsonContext.Default.Boolean);
            }

            case HostOps.VarClear:
                host.ClearVariables(Scope(Args(argsJson, HostJsonContext.Default.VarArgs)));
                return "null";
            case HostOps.VarList:
                return Json(Map(host.ListVariables(Scope(Args(argsJson, HostJsonContext.Default.VarArgs)))), HostJsonContext.Default.ListKeyValueDto);
            case HostOps.VarReplace:
                return Json(host.ReplaceIn(Args(argsJson, HostJsonContext.Default.VarArgs).Value ?? ""), HostJsonContext.Default.String);
            case HostOps.ReqGet:
                return Json(ToDto(host.Request), HostJsonContext.Default.RequestDto);
            case HostOps.ReqSet:
                Apply(host.Request, Args(argsJson, HostJsonContext.Default.RequestDto));
                return "null";
            case HostOps.ResGet:
                return host.Response is { } r ? Json(ToDto(r), HostJsonContext.Default.ResponseDto) : "null";
            case HostOps.InfoGet:
            {
                var i = host.Info;
                return Json(new InfoDto(i.EventName, i.RequestName, i.RequestId.ToString(), i.Origin), HostJsonContext.Default.InfoDto);
            }

            case HostOps.Console:
            {
                var c = Args(argsJson, HostJsonContext.Default.ConsoleArgs);
                collector.AddConsole(Enum.TryParse<ConsoleLevel>(c.Level, true, out var level) ? level : ConsoleLevel.Log, c.Text);
                return "null";
            }

            case HostOps.Test:
            {
                var t = Args(argsJson, HostJsonContext.Default.TestArgs);
                collector.AddTest(t.Name, t.Passed, t.Message, t.Ms);
                return "null";
            }

            case HostOps.Send:
                return Json(Send(Args(argsJson, HostJsonContext.Default.RequestDto)), HostJsonContext.Default.SendResultDto);
            case HostOps.Cookies:
                return Json(host.ResponseCookies.Select(ToDto).ToList(), HostJsonContext.Default.ListCookieDto);
            case HostOps.Unsupported:
                throw new NotSupportedException($"pm.{Args(argsJson, HostJsonContext.Default.String)} is not supported in Yaapic");
            default:
                throw new InvalidOperationException($"Unknown host op '{op}'");
        }
    }

    private SendResultDto Send(RequestDto dto)
    {
        ScriptRequest request = new();
        Apply(request, dto);
        var result = host.Send(request);
        return new(result.Response is null ? null : ToDto(result.Response), result.Error);
    }

    private static T Args<T>(string json, JsonTypeInfo<T> info) =>
        JsonSerializer.Deserialize(json, info) ?? throw new JsonException("Missing arguments");

    private static string Json<T>(T value, JsonTypeInfo<T> info) => JsonSerializer.Serialize(value, info);

    private static string Name(VarArgs a) =>
        string.IsNullOrEmpty(a.Name) ? throw new ArgumentException("Variable name is required") : a.Name;

    private static ScriptScope Scope(VarArgs a) => a.Scope switch
    {
        "layered" => ScriptScope.Layered,
        "local" => ScriptScope.Local,
        "environment" => ScriptScope.Environment,
        "collection" => ScriptScope.Collection,
        "globals" => ScriptScope.Globals,
        _ => throw new ArgumentException($"Unknown scope '{a.Scope}'"),
    };

    private static List<KeyValueDto> Map(IEnumerable<KeyValueEntry> rows) => [.. rows.Select(e => new KeyValueDto(e.Key, e.Value, e.Enabled))];

    private static RequestDto ToDto(ScriptRequest r) =>
        new(r.Method, r.Url, Map(r.Headers), r.BodyMode, r.BodyText, r.BodyContentType, Map(r.UrlEncoded), Map(r.FormData));

    private static ResponseDto ToDto(ScriptResponse r) =>
        new(r.Code, r.Status, Map(r.Headers), r.BodyText, r.SizeBytes, r.ResponseTimeMs, r.FinalUrl, [.. r.Cookies.Select(ToDto)]);

    private static CookieDto ToDto(ScriptCookie c) =>
        new(c.Name, c.Value, c.Domain, c.Path, c.Expires, c.HttpOnly, c.Secure, c.SameSite);

    private static void Apply(ScriptRequest target, RequestDto dto)
    {
        target.Method = dto.Method;
        target.Url = dto.Url;
        target.BodyMode = dto.BodyMode;
        target.BodyText = dto.BodyText;
        target.BodyContentType = dto.BodyContentType;
        Replace(target.Headers, dto.Headers);
        Replace(target.UrlEncoded, dto.UrlEncoded);
    }

    private static void Replace(IList<KeyValueEntry> list, List<KeyValueDto>? rows)
    {
        list.Clear();
        foreach (var row in rows ?? [])
        {
            list.Add(new(row.Key, row.Value, row.Enabled));
        }
    }
}
