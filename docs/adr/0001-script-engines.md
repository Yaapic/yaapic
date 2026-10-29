# ADR 0001: Script engines

Status: Accepted

## Context

Pre-request and test scripts must run inside a Native AOT application, sandboxed, on Windows, macOS and Linux. The
engine must not need reflection or runtime code generation.

## Options

1. **QuickJS-NG (JavaScript) and Lua 5.4 behind `IScriptEngine`.** Small C libraries, no JIT, embeddable via P/Invoke.
2. **Jint (JavaScript in C#).** Managed, but relies on reflection and expression trees for CLR interop; poor Native AOT fit.
3. **Roslyn scripting (C#) or IronPython.** Need runtime code generation; not AOT compatible.

## AOT risks

Options 2 and 3 break the Native AOT rules. Option 1 ships a native library per RID and binds it with source-generated
`LibraryImport` P/Invokes (no marshalling reflection). Risk: native binaries must be built or sourced for every
supported RID and kept up to date.

## Decision

Option 1. JavaScript (QuickJS-NG, Postman-compatible `pm.*` API) and Lua 5.4, both behind one `IScriptEngine` port in
`Yaapic.Core`. No C# and no Python scripting. Engines are wired in milestone 3.
