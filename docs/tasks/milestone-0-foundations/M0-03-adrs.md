# M0-03: ADRs 0001-0007

Status: blocked
Platform: any
Depends on: M0-01

## Goal

One ADR per technical decision in `docs/adr/`: script engines, code editor, dynamic gRPC, HTTP/3, storage format,
shared design tokens, Git client. Each lists 2 to 3 options, AOT risks and the decision.

## Design decisions

Decided: scripting = JavaScript (QuickJS-NG) and Lua behind `IScriptEngine`; no C# or Python.
Open (the card stays `blocked` until answered): storage format (YAML or JSON), Git client (git binary or
LibGit2Sharp), SDK version (.NET 11 RC or .NET 10 LTS).

## Out of scope

Prototypes (they come with the milestone that needs them).

## Acceptance criteria

- [ ] Seven ADRs in English, each with status, options, risks and decision.
- [ ] `docs/adr/README.md` index.

## Suggested commit message

`docs: add architecture decision records 0001-0007`
