# M3-67: Milestone 3 docs

Status: todo
Platform: any
Depends on: M3-64, M3-65, M3-66, M3-32, M3-49

## Goal

Bring every document in line with the code and close the milestone summary.

## Design decisions

- `docs/architecture.md`: new project `Yaapic.Scripting` in the references table, the scripted request flow (diagram below the
  existing one: `RequestEditorViewModel.SendCommand -> ScriptedRequestSender -> chain (collection, folders, request) -> pre-request
  runs -> RequestSender -> post-response runs -> ScriptedSendReport -> ResponsePanelViewModel / ConsoleViewModel /
  IScriptVariableApplier`), engines and the `__host` bridge, the vault (ports, `FileSecretVault`, decorators, redactor, protectors),
  the new view models (`ScriptsEditorViewModel`, `ConsoleViewModel`, `TestResultsViewModel`, `VaultViewModel`).
- `docs/file-format.md`: check `scripts`, `secret: true` and `.yaapic/workspace.id` against the files the E2E tests produced; fix any
  difference; add the vault file shape as a non-hand-edited, per-user file with a link to ADR 0010.
- `docs/dependencies.md`: QuickJS-NG, Lua, the Lua shim, `System.Security.Cryptography.ProtectedData` rows are present and
  consistent (versions, licences, AOT results); a milestone 3 line (`added: ...`).
- `docs/scripting-compatibility.md`: final pass (JavaScript and Lua tables match the conformance suites; known differences).
- `docs/adr/0009`, `0010`: status `Accepted`, add an "Implementation notes (M3)" section listing deviations found while building
  (read the Notes of the M3 cards), and update `docs/adr/0001` "See also".
- `README.md` (root) and `docs/` contributor docs: the native build prerequisite (`pwsh native/Build-Natives.ps1`, a C compiler,
  CMake), how the scripts run, where the vault lives.
- `docs/tasks/README.md`: replace the M3 `planned` row in the milestone table with `detailed (68 cards, see above)`; set every
  M3 card status to its real value; add the **Milestone 3 summary** under the M3 table (delivered features, deferred list below);
  mark M3 closed when the last card is done.
- Deferred list to write (from ADRs 0009 and 0010): C#/Python scripting, npm/`require`, debugger, snippets sidebar, timers,
  `pm.vault`, `pm.execution.setNextRequest` (Runner, M6), JSON schema assertions, cookie jar and Cookies tab (milestone 4 or later),
  `pm.request.auth`, secret creation from the variable popup, auth-field secrets helper, changing the vault passphrase or protector,
  secret sharing between machines, request-level variables, Postman script import (M5).

## Out of scope

New features; screenshots (M3-68).

## Acceptance criteria

- [ ] Every document above matches the code (spot-check each claim against the source; list what was checked in Notes).
- [ ] `docs/tasks/README.md` has the M3 table, summary and deferred list; no card of M3 left with a wrong status.
- [ ] 0 warnings, all tests green.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`docs: add milestone 3 docs and summary`
