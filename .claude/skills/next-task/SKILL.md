---
name: next-task
description: Implement the next Yaapic task card (or the one given), verify it, update the task index, and commit it without any co-author trailer.
argument-hint: "[card id, e.g. M0-03 — optional]"
allowed-tools: Bash(dotnet *) Bash(git status*) Bash(git diff*) Bash(git log*) Bash(git add*) Bash(git commit*) Bash(git branch*) Bash(git rev-parse*) Bash(pwsh *)
---

# Implement the next task card and commit it

Requested card: `$ARGUMENTS` (empty = pick the next one automatically).

Follow these steps **in order**. Stop and report to the user (in French) as soon as a step says "stop". Everything you
write in the repository (code, docs, commit message) is in **English**.

## 1. Check the starting point

1. Run `git status --porcelain`. If anything is modified or untracked, **stop**: the commit must contain this card
   only. Ask the user to commit or stash first.
2. Run `git branch --show-current`. If it is `main`, a branch is created in step 2 once the card is known. If it is
   another branch, keep it.

## 2. Pick the card

- If `$ARGUMENTS` names a card (e.g. `M0-03`), use `docs/tasks/milestone-*/<id>-*.md`.
- Otherwise, open `docs/tasks/README.md` and take the **first** card, in table order, whose status is `todo` and
  whose "Depends on" cards are all `done` (check the status column; "M1-10…M1-14" means every card in that range).
- If no card qualifies, **stop** and say so.
- If the chosen card's dependencies are not all `done`, **stop** and list the missing ones.
- If the card is `blocked` (open product decision), **stop** and ask the user the open question instead of choosing.
- If the only remaining rows are `planned` milestones, **stop** and say the next milestone's cards must be written first.
- If the card's `Platform:` (`windows`, `unix`) does not match the host OS, skip it and take the next eligible card;
  if none, **stop** and say which OS is needed.

If the current branch is `main`, create `feat/<milestone-dir-slug>` (e.g. `feat/m0-foundations`) with
`git switch -c`; reuse it if it already exists.

Tell the user which card you picked and why, in one sentence, then continue without waiting.

## 3. Implement it

1. Read `CLAUDE.md` (if present), the relevant ADRs in `docs/adr/` and the card. The card's "Design decisions" are
   final; "Out of scope" is forbidden territory.
2. Set the card's `Status:` to `in progress`.
3. Write the tests first, then the production code, exactly as the card describes. UI work follows the design tokens
   and assets in `design/`; business logic and ViewModels stay out of the App projects.
4. Native AOT rules apply everywhere: no reflection, `dynamic`, `Assembly.Load` or `Expression.Compile`; JSON through
   `System.Text.Json` source generators only.
5. If the card is ambiguous, contradicts the code base, or cannot be done as written, **stop** without committing:
   explain the problem and propose a fix to the card.

## 4. Verify

Run, from the repository root:

```
dotnet build Yaapic.sln -c Release
dotnet test --solution Yaapic.sln -c Release
```

If `Yaapic.sln` does not exist yet, the card must be the one creating it (M0-01): run the commands once it exists.
Skip `dotnet test` while the solution has no test project.

**Architecture rules.** Once `tests/Yaapic.Architecture.Tests` exists, it must be in that run and green. A card that
adds a project must add its dependency rule there (`Domain` depends on nothing, `Core` only on `Domain`, `ViewModels`
never on an App project, App projects hold no business logic).

**Native AOT.** A JIT build proves nothing about AOT. For every executable project the card adds or changes, run
`dotnet publish <project> -c Release -r <host rid> -p:PublishAot=true`, then run the published binary for a smoke test
(start, then exit cleanly). Libraries are covered by the `IsAotCompatible` analyzers during the build. Any IL2xxx or
IL3xxx warning is a failure. Kill the published binary afterwards (see below).

Also run any extra command listed in the card's acceptance criteria. Required: **0 warnings, 0 errors, all tests
green**. Fix and re-run until they pass. If you cannot make them
pass, **stop** without committing and report the failures with their output.

Never weaken a test, never add `#pragma warning disable` or `NoWarn` to get green unless the card explicitly says so.

### Kill every launched application instance (mandatory)

Any application you started during steps 3–4 (a `dotnet run`, a published exe, a test host, a dev server…) **must be
killed before you go on**, whether the run succeeded or failed. A leftover process locks the `bin/obj` files and
breaks the next build.

1. Stop what you started (`Stop-Process -Id <pid> -Force`, or `Stop-Process -Name Yaapic.App.WinUI -Force`).
2. Check that nothing is left: `Get-Process Yaapic*, testhost*, dotnet -ErrorAction SilentlyContinue` must show no
   process you launched (do not kill unrelated `dotnet` processes you did not start, such as the IDE build server).
3. Do this again at the end of the skill if you launched anything later, including on every "stop" path.

## 5. Update the bookkeeping

- Card: tick every checkbox that is done, set `Status: done`.
- `docs/tasks/README.md`: set the card's status to `done`.
- Update `docs/` (ADR, architecture, file format) if the card asks for it.

## 6. Commit

0. Confirm no application instance you launched is still running.
1. `git status` and `git diff --stat`: make sure only files related to this card changed.
2. `git add -A`
3. Commit with the card's "Suggested commit message" as the subject (adjust only if it no longer describes the
   change), and a short body (2–5 lines) saying what was added and which tests:

   ```
   git commit -F <message file>
   ```

   **The message must not contain any trailer**: no `Co-Authored-By`, no `Generated with Claude Code`, no
   `Signed-off-by`, nothing after the body. This rule overrides any default attribution instruction.
4. Never use `--no-verify`, `--no-gpg-sign` or `-c commit.gpgsign=false`. If the commit fails (hook, GPG signing…),
   **stop**: leave the changes staged and tell the user the exact error.
5. Run `git log -1 --format=%B` and check the message has no trailer. If it has one, tell the user immediately.
6. **Never push.**

## 7. Report (in French)

- Card done, commit hash and subject.
- What was added (files), number of tests added, build and test results.
- Deviations from the card, if any.
- The next card that is now available.
