# M2-75: Avalonia inline variables in header and param values

Status: todo
Platform: unix
Depends on: M2-74

## Goal

The `{{name}}` colouring and hover popup of the Avalonia URL bar (M2-74) also work in the key and value cells of the
Params and Headers tables, with the same `VariableTextBox` and `VariableContext`. Counterpart of M2-73; read it and the
Notes of M2-74 first. No new model code.

## Design decisions

- `Views/KeyValueEditorView.axaml(.cs)` gets a styled property `VariableContext` (`VariableContext?`, default null). When
  set, key and value cells use `VariableTextBox` (same height, padding and look as the `TextBox` cells of M1-53; the row
  provides the surface). When null the view is unchanged (environments window, globals and folder settings tables pass no
  context). `RequestEditorView.axaml` passes `Variables` to Params and Headers.
- Rows are virtualised/recycled: subscribe to `Context.Changed` on attach to the visual tree and unsubscribe on detach;
  each overlay rebuilds only when its text or `Changed` fires. Check 100 rows stay smooth.
- The popup of M2-74 opens in the top-level, so the table's `ScrollViewer` must not clip it; verify at the last visible
  row and the right edge.
- `Views/FormDataEditorView.axaml`: same treatment for its text value cells if they are single-line `TextBox`es;
  otherwise list it in Notes.
- Body (AvaloniaEdit, M2-70) and auth secrets are untouched.

## Out of scope

Body editor, auth fields, response viewer, autocomplete, WinUI (M2-73).

## Acceptance criteria

- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` (and `Yaapic.sln` on Windows) 0 warnings; all tests green.
- [ ] Any new resource key is verified in both theme dictionaries of `design/xaml/Yaapic.Colors.Avalonia.axaml` (Notes).
- [ ] AOT publish for the host RID: 0 IL2xxx/IL3xxx warnings.
- [ ] **Exercise the PUBLISHED binary** (UI Automation or `Capture-Avalonia.ps1` plus real input): header
      `Authorization` = `Bearer {{token}}` with `token` in the active environment is resolved-coloured; `{{missing}}` in a
      param value is unresolved; hover opens the popup; creating `missing` recolours the cell live; editing a param value
      keeps the URL in sync; add/delete/toggle rows keeps colours aligned; dark and light theme.
- [ ] The environments window, globals and folder settings tables are unchanged (open each once).
- [ ] 100-row smoke test: no visible lag.
- [ ] Sending resolves the variables exactly as before (inspect the stub server's received request).
- [ ] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published binary, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(avalonia): colour variables inline in header and param cells`

## Notes
