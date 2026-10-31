# ADR 0004: HTTP/3 and .NET SDK version

Status: Accepted (SDK), Proposed (HTTP/3 scope, confirmed in M4)

## Context

Yaapic sends requests with `HttpClient`. HTTP/3 needs QUIC (`System.Net.Quic`), which depends on `msquic` and is not
available on every OS. The SDK choice decides which runtime and AOT improvements we get.

## Options: HTTP/3

1. **`HttpClient` with `HttpVersionPolicy.RequestVersionOrLower`, HTTP/3 as an opt-in setting**, greyed out when
   `QuicConnection.IsSupported` is false.
2. **Always request HTTP/3 first.** Breaks on machines without QUIC.
3. **Third-party QUIC stack.** Large native dependency for no gain.

## Options: SDK

1. **.NET 11 RC (then GA).** Latest AOT and `System.Net` work.
2. **.NET 10 LTS.** Stable and supported, older.

## AOT risks

`HttpClient` and `System.Net.Quic` are AOT compatible. The msquic native library must be present at run time, so the
feature is detected, never assumed.

## Decision

HTTP/3: option 1. SDK: **.NET 11 RC**, chosen by the product owner. Accepted risk: a pre-release SDK can change or
regress between RC builds and its packages may not be supported; Windows App SDK and Avalonia compatibility must be
re-verified on each RC and at GA. Fallback: return to .NET 10 LTS. `global.json` and the target frameworks moved to
.NET 11 RC3 together with C# 15.
