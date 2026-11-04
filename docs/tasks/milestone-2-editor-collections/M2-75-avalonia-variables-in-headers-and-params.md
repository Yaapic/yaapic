# M2-75: Avalonia inline variables in header and param values

Status: done
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

- [x] `dotnet build Yaapic.CrossPlatform.slnf -c Release` (and `Yaapic.sln` on Windows) 0 warnings; all tests green.
- [x] Any new resource key is verified in both theme dictionaries of `design/xaml/Yaapic.Colors.Avalonia.axaml` (Notes).
- [x] AOT publish for the host RID: 0 IL2xxx/IL3xxx warnings.
- [x] **Exercise the PUBLISHED binary** (UI Automation or `Capture-Avalonia.ps1` plus real input): header
      `Authorization` = `Bearer {{token}}` with `token` in the active environment is resolved-coloured; `{{missing}}` in a
      param value is unresolved; hover opens the popup; creating `missing` recolours the cell live; editing a param value
      keeps the URL in sync; add/delete/toggle rows keeps colours aligned; dark and light theme.
- [x] The environments window, globals and folder settings tables are unchanged (open each once).
- [x] 100-row smoke test: no visible lag.
- [x] Sending resolves the variables exactly as before (inspect the stub server's received request).
- [x] No Yaapic or stub-server process left running.

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

`KeyValueEditorView` gets the `VariableContext` styled property; its key and value cells are always a `VariableTextBox`
inside a `Border.cellhost` (8 px padding plus the 2 px border that turns into the focus ring on `:focus-within`, which
reproduces the former `TextBox.cell` look: 10 px text inset, code font). With a null context the box is a plain text box, so
the environments and globals tables look as before. `RequestEditorView.axaml` passes `Variables` to Params and Headers
(through `$parent[RequestEditorView]`, because the view's own `DataContext` is the table). The URL-encoded body table
passes no context.
No `ItemsControl` virtualisation exists here, so each overlay subscribes to `Context.Changed` on attach and unsubscribes on
detach (already in `VariableTextBox`); 100 rows load and scroll without lag.

**Not done:** `FormDataEditorView` keeps its plain `TextBox` value cells: its value cell is read-only for file rows, which
`VariableTextBox` does not support yet. The folder settings table does not exist on Avalonia (the Collection editor is
M2-51, `todo`), so there was nothing to open for it. No new resource key (only `YaapicFocusRingBrush`, already used).

**Verification (published Native AOT exe on Windows, real mouse and `SendKeys` input, screen captures, loopback stub that
logs the received request):** build and publish 0 warnings and 0 IL2xxx/IL3xxx; `Authorization` = `Bearer {{token}}` green,
`{{missing}}` in a param value amber; hover popups (value + Environment badge; unresolved form with scope list) not clipped by
the table, also on the last visible row of a scrolled 100-row table; created `missing` in the active environment from the
cell popup and both the cell and the URL turned green live; editing a param value (`1` to `17`) updated the URL; typing,
caret and selection (`{{t` selected) in a cell normal; add, toggle and delete rows keep colours aligned; light and dark;
environments and globals tables unchanged; the Send button hit the stub with
`GET /users?page=17&q=zzz` and `Authorization: Bearer abc123`; 100 rows smooth. The right-edge popup case was not
reproduced: tokens sit at the left of wide cells and the popup is anchored left. All launched processes were stopped.
