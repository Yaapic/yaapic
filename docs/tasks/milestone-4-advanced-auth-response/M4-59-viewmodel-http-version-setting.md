# M4-59: ViewModel: HTTP version in the request settings

Status: done
Platform: any
Depends on: M4-38, M4-39

## Goal

The request editor exposes the HTTP version preference, and the choice of HTTP/3 is disabled when the machine cannot do it.

## Design decisions

- Read first: ADR 0014 (HTTP version), `RequestEditorViewModel.cs` (`FollowRedirects`, `TimeoutSeconds`, `ToDefinition`, `Load`, dirty tracking), M4-38, M4-39.
- `RequestEditorViewModel` gets an observable `HttpVersion` (`HttpVersionPreference`) read by `ToDefinition` into `RequestSettings` and loaded by `FromDefinition`; edits
  mark the tab dirty exactly like `FollowRedirects`.
- Static list `HttpVersionChoices` of `HttpVersionChoice(HttpVersionPreference Value, string Label, bool IsAvailable)`: `Default` ("Automatic (HTTP/1.1 or HTTP/2)"),
  `Http1` ("HTTP/1.1"), `Http2` ("HTTP/2"), `Http3` ("HTTP/3 (experimental)"); `Http3.IsAvailable` comes from `IHttpVersionSupport.IsHttp3Supported`, passed to the editor
  through the factories as an optional parameter `IHttpVersionSupport? versionSupport = null` (null means HTTP/3 unavailable). Labels are localized strings.
- A request loaded with `Http3` on a machine without support keeps the value (the file is not rewritten) but `HttpVersionNote` shows
  `Strings.HttpVersionHttp3Unavailable` ("HTTP/3 is not available on this machine; HTTP/2 is used.").
- Strings (EN and FR): the four labels, the note, `RequestSettingsHttpVersion`.

## Out of scope

Views (M4-69, M4-77).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: load/save round trip of each value; dirty tracking; `Http3` availability true and false (and with no support object); the note
  appears only for a loaded `Http3` while unavailable; labels present in both languages (the Localization tests already check key parity).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the HTTP version setting to the request editor`
