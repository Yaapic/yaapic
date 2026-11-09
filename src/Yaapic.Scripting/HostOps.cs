namespace Yaapic.Scripting;

/// <summary>The operation names of the <c>__host(op, argsJson)</c> bridge (the contract of ADR 0009).</summary>
internal static class HostOps
{
    public const string VarGet = "var.get";
    public const string VarSet = "var.set";
    public const string VarUnset = "var.unset";
    public const string VarHas = "var.has";
    public const string VarClear = "var.clear";
    public const string VarList = "var.list";
    public const string VarReplace = "var.replace";
    public const string ReqGet = "req.get";
    public const string ReqSet = "req.set";
    public const string ResGet = "res.get";
    public const string InfoGet = "info.get";
    public const string Console = "console";
    public const string Test = "test";
    public const string Send = "send";
    public const string Cookies = "cookies";
    public const string Unsupported = "unsupported";
}
