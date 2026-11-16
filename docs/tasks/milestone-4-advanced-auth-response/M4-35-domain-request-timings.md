# M4-35: Domain: request timings

Status: todo
Platform: any
Depends on: M4-03

## Goal

A response can carry the timing phases of its request.

## Design decisions

- Read first: ADR 0014 (Timings), `src/Yaapic.Domain/Http/HttpResponseData.cs`.
- `public sealed record RequestTimings(TimeSpan? Dns, TimeSpan? Connect, TimeSpan? Tls, TimeSpan Wait, TimeSpan Download, bool
  ConnectionReused)` in `Yaapic.Domain.Http`, with:
  - `TimeSpan Sum` = every non-null phase added (the measured part of the total);
  - `static RequestTimings Empty` (all zero, `ConnectionReused` false, nulls for the three connection phases).
- `HttpResponseData` gains `public RequestTimings? Timings { get; init; }` (an init property, **not** a new positional parameter, so every
  constructor call stays valid). `HttpResponseData` equality, `IsEquivalentTo` and anything that copies a response (for example script
  response snapshots) keep working: grep the solution for `new HttpResponseData(` and `with {` on responses and confirm none loses the
  property when it should keep it (a response rebuilt by `ScriptedRequestSender` after scripts must keep `Timings`).
- `HttpResponseData` also gains `public string Protocol { get; init; } = ""` here (the used HTTP version, filled by M4-39); empty means
  unknown.

## Out of scope

Measuring (M4-36), display (M4-45).

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: `Sum` with and without null phases; `Empty`; `HttpResponseData` created without timings has
  `Timings == null` and `Protocol == ""`; record equality includes both.
- [ ] A test in `Yaapic.Core.Tests` proves a scripted send keeps `Timings` and `Protocol` of the transport response.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add request timings and the protocol to responses`
