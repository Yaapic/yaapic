# M2-08: YAML request serializer: methods, bodies, auth and settings

Status: done
Platform: any
Depends on: M2-04

## Goal

The request file carries everything the extended `HttpRequestDefinition` holds, as specified in `docs/file-format.md`
(M2-01).

## Design decisions

- `RequestFileDto` gains `Auth` (`AuthDto`) and `Settings` (`SettingsDto`); `BodyDto` gains `Fields`
  (`List<FormFieldDto>`: `Key`, `Value`, `Enabled`, `Kind`) and `File`. `AuthDto.Location` is written as `in` (use
  `[YamlMember("in")]`). Enums stay strings converted by hand: methods through `HttpMethodKindExtensions` (M2-02),
  body `none|raw|urlencoded|form-data|binary`, content types `text|json|xml|html|javascript`, auth
  `none|basic|bearer|apikey`, field kind `text|file`, location `header|query`.
- Serialize: keys in the documented order, every key written (empty lists as `[]`, empty strings as `""`).
- Deserialize: missing `auth` -> `RequestAuth.None`, missing `settings` -> `RequestSettings.Default`, missing
  `fields`/`file` -> empty, missing `enabled` -> true, missing field `kind` -> text. An unknown method, body type,
  content type, auth type, field kind or location throws `StorageException` naming the value. A negative
  `timeoutSeconds` throws `StorageException`.
- Golden files: regenerate `tests/Yaapic.Infrastructure.Tests/Storage/golden/request-v1.request.yaml` from a fixture that
  uses every new key; rename the current M1 file to `request-m1-legacy.request.yaml` and keep a test that reads it
  (missing keys take defaults) without rewriting it.

## Out of scope

Folder and globals files (M2-27, M2-41), file system access.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: round trip equivalence for a request using PUT, urlencoded fields,
      form-data with a file field, binary, each auth kind and non-default settings; golden match byte for byte; the legacy
      M1 file reads with defaults; each unknown value throws; unicode and `{{variables}}` survive.
- [x] No new reflection (AOT); 0 warnings.

## Suggested commit message

`feat(infrastructure): persist methods, body kinds, auth and settings in request files`
