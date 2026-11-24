# M5-28: Core: Insomnia import, authentication and template tags

Status: done
Platform: any
Depends on: M5-27

## Goal

Insomnia `authentication` objects become `RequestAuth` (inheritance from folders is not an Insomnia concept in v4: request auth only), and `{{ _.var }}` template syntax becomes `{{var}}`.

## Design decisions

- Read first: M5-27, M5-14 (target auth shapes, same defaults), `src/Yaapic.Domain/Http/RequestAuth.cs`, `docs/file-format.md` (Authentication).
- `src/Yaapic.Core/Import/Insomnia/InsomniaAuth.cs` (internal static): `RequestAuth Map(DataNode? authentication, string path, ImportReport report)`; `authentication` absent, empty object, `type` empty or `disabled: true` -> `RequestAuth.None`.
  - `type` mapping (Insomnia keys -> Yaapic): `basic` -> Basic (`username`, `password`); `bearer` -> Bearer (`token`; `prefix` other than empty or `Bearer` -> `Warning`, ignored); `apikey` -> ApiKey (`key`, `value`, `addTo` `queryParams` -> Query else Header);
    `digest` -> Digest (`username`, `password`); `iam` -> AwsV4 (`accessKeyId`, `secretAccessKey`, `sessionToken`, `region`, `service`); `oauth2` -> OAuth2: `grantType` `client_credentials` -> ClientCredentials, `authorization_code` -> AuthorizationCode, `password` -> Password,
    others (`implicit`, `jwt-bearer`) -> `Warning` and no auth; `accessTokenUrl` -> TokenUrl, `authorizationUrl` -> AuthorizationUrl, `clientId`, `clientSecret`, `scope`, `audience`, `username`, `password`, `redirectUrl` -> RedirectUri, `credentialsInBody` true -> `ClientAuthentication.Body` else BasicHeader,
    `Placement` Header, `HeaderPrefix` = `headerPrefix` (default `Bearer`); `none` -> None; anything else (`ntlm`, `hawk`, `oauth1`, `asap`, `netrc`) -> `Warning` `Strings.ImportAuthUnsupported(path, type)` and no auth.
  - Literal credentials trigger the shared `Info` `Strings.ImportAuthPlainText` once per import (same rule as M5-14: when a value is not empty and not a `{{variable}}`; apply it **after** the template conversion below).
- `src/Yaapic.Core/Import/Insomnia/InsomniaTemplates.cs` (internal static): `string Translate(string text, string path, ImportReport report)` applied to **every string field** the importer copies (URL, param, header and form values and names, body text, auth values, folder and variable values):
  - `{{ _.name }}` and `{{_.name}}` -> `{{name}}`; `{{ _['name'] }}` and `{{ _["name"] }}` -> `{{name}}`; nested `{{ _.a.b }}` -> `{{a.b}}` (Yaapic variable names may contain dots).
  - `{% uuid 'v4' %}` and `{% uuid 'v4' %}` with any spacing -> `{{$guid}}`; `{% now 'iso-8601' %}` and `{% now 'iso-8601', '' %}` -> `{{$isoTimestamp}}`; `{% now 'unix' %}` and `{% now 'unix', '' %}` -> `{{$timestamp}}`.
  - Any other `{% name ... %}` tag (including `response`, `base64`, `faker`, `prompt`, `hash`, `jsonpath`, `file`, `now` with another format, `uuid` with another version) is left **unchanged** and `report.AddOnce(Warning, path, Strings.ImportInsomniaTemplateTag(name))`.
  - Any other `{{ expression }}` that is not `_...` (for example `{{ response ... }}` in older exports) is left unchanged and warned once per expression text (`Strings.ImportInsomniaTemplateTag`).
  - Scan with a small hand-written scanner or `[GeneratedRegex]` (no reflection); text without `{{` or `{%` is returned as is.
- `InsomniaRequests` (M5-27) is extended: every copied string goes through `Translate`; `Auth = InsomniaAuth.Map(...)`.
- Strings (EN and FR): `import.insomnia.templateTag` = "The Insomnia template \"{0}\" has no Yaapic equivalent and was left as written."; `import.authUnsupported` and `import.authPlainText` (M5-14).

## Out of scope

Environments (M5-29), scripts.

## Acceptance criteria

- [x] Tests: each auth type of the table with exact `RequestAuth`; disabled auth; empty type; unsupported grant and `ntlm` warn; bearer prefix warning; template conversions (`_.name`, `_['name']`, nested, spacing variants, `uuid`, `now` formats, `response` tag warned once per distinct tag, plain text unchanged);
  a request with `{{ _.baseUrl }}/users` imports as `{{baseUrl}}/users`; the plain-text `Info` appears once and not for `{{token}}` values.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Insomnia authentication and template tags`
