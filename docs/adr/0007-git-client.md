# ADR 0007: Git client

Status: Accepted

## Context

Collaboration (milestone 8) needs clone, commit, branch, diff, merge and push on workspace folders, with the user's
own credentials, SSH keys and Git configuration.

## Options

1. **The `git` binary, run as a child process.** Honours the user's credential helpers, SSH agent, hooks and config,
   with the exact behaviour of the real tool.
2. **LibGit2Sharp.** In-process API, but a native libgit2 per RID, a lagging feature set (partial SSH and credential
   helper support) and uncertain Native AOT support.
3. **Pure managed Git implementation.** Immature and incomplete.

## AOT risks

Option 1 has none: process start and text parsing only. Risk: Git must be installed; the app detects it and explains
how to install it. Output is parsed in porcelain or `-z` formats to stay locale independent.

## Decision

Option 1, behind an `IGitClient` port in `Yaapic.Core` so the implementation can be replaced or faked in tests.
