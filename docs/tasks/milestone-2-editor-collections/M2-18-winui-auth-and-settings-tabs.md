# M2-18: WinUI Auth and Settings tabs

Status: done
Platform: windows
Depends on: M2-11, M2-16

## Goal

The request editor gets an Auth tab and a Settings tab.

## Design decisions

- Two more entries in the editor section `SelectorBar`, after Body: "Auth" and "Settings". The Auth entry shows a dot
  badge when `Auth.IsActive`.
- `Views/AuthEditorView.xaml` (`UserControl`, `ViewModel` of type `AuthEditorViewModel`): a type `ComboBox` (None,
  Basic Auth, Bearer Token, API Key; index map in code-behind) and below it the fields for the selected kind:
  Basic: Username, Password (`PasswordBox`, two-way to `Password`); Bearer: Token (`PasswordBox`-style masked
  `PasswordBox`); API Key: Key, Value, and "Add to" (Header / Query Params, index map). Each field has a header text
  and the placeholder "Use {{variable}} to keep secrets out of the file". `None` shows "This request does not use any
  authentication."
- Settings pane in `RequestEditorView.xaml`: a `ToggleSwitch` "Follow redirects" (`FollowRedirects`, two-way) and a
  `NumberBox` "Timeout (seconds, 0 = default)" (`TimeoutSeconds`, minimum 0, spin buttons inline, value converted to
  `int`).
- Visibility of the per-kind field groups through `VisibilityMap` functions of the `AuthKind` index; `x:Bind` only.
- Use the control styles of M1-36/M1-37; compare with the Params pane spacing.

## Out of scope

OAuth 2.0 and other auth types (M4), a global proxy or TLS setting.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe: Basic auth with `user` / `pass` against `https://httpbin.org/basic-auth/user/pass`
      returns 200; a wrong password returns 401; Bearer shows in `https://httpbin.org/bearer`; with Follow redirects off,
      `https://httpbin.org/redirect/1` returns 302.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add authentication and settings tabs to the request editor`
