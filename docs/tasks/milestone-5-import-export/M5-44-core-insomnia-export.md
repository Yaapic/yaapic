# M5-44: Core: Insomnia v4 export

Status: done
Platform: any
Depends on: M5-05, M5-36

## Goal

`InsomniaExporter` writes a collection (folders, requests, variables, environments) as an Insomnia v4 JSON export.

## Design decisions

- Read first: M5-27, M5-28, M5-29 (the import side: this card is its inverse; read their field names), M5-36.
- `src/Yaapic.Core/Export/Insomnia/InsomniaExporter.cs` (`public sealed class InsomniaExporter : IExporter`, `Id` `insomnia`, `DisplayName` `Insomnia (v4 JSON)`, `FileExtension` `.json`, `RequiresCollection` true) and `InsomniaRequestWriter.cs`, `InsomniaTemplateWriter.cs` (internal static).
  File: `SafeFileName(source.Name) + ".insomnia.json"`, content `DataNodeJson.Write(tree, true) + "\n"`.
- **Document**: `{ "_type": "export", "__export_format": 4, "__export_source": "yaapic", "resources": [ ... ] }` (no `__export_date`: not deterministic). Resource ids: `wrk_` / `fld_` / `req_` / `env_` + the Yaapic GUID in "N" format (32 hex digits).
  Resource order: the workspace, the base environment, the sub environments, then the folders and requests depth first in store order.
- **workspace**: `{ "_id", "_type": "workspace", "parentId": null, "name": collection name, "description": collection description, "scope": "collection" }`.
- **Base environment**: `{ "_id": "env_<collection id N>", "_type": "environment", "parentId": <workspace id>, "name": "Base Environment", "data": { <key>: <value string> for each enabled collection variable }, "metaSortKey": 0 }`; written even when empty. Disabled rows are dropped, secrets are blank (M5-36).
  **Sub environments**: each `source.Environments` entry -> `{ "_id": "env_<id N>", "_type": "environment", "parentId": <base environment id>, "name", "data": {...enabled rows}, "metaSortKey": <1-based position> }`.
- **Folders**: `{ "_id", "_type": "request_group", "parentId": <parent folder or workspace id>, "name", "description", "metaSortKey": <0-based position among siblings> }`.
- **Requests**: `{ "_id", "_type": "request", "parentId", "name", "description": "", "method", "url" (stored URL with the **enabled** params composed in), "parameters": [], "headers": [ { "name", "value", "disabled"? } ], "body": {...}, "authentication": {...}, "settingFollowRedirects": "on"|"off", "metaSortKey" }`.
  `parameters` is written empty because the query is already in `url`; but **disabled** params would be lost, so they are written to `parameters` as `{ "name", "value", "disabled": true }`. `disabled` is written only when true. Member order as listed.
  - **body**: `Raw` -> `{ "mimeType": "application/json"|"application/xml"|"text/html"|"application/javascript"|"text/plain", "text": text }`; `UrlEncoded` -> `{ "mimeType": "application/x-www-form-urlencoded", "params": [ { "name", "value", "disabled"? } ] }`;
    `FormData` -> `{ "mimeType": "multipart/form-data", "params": [ text: { "name", "value", "disabled"? } | file: { "name", "value": "", "type": "file", "fileName": path, "disabled"? } ] }`; `Binary` -> `{ "mimeType": "application/octet-stream", "fileName": path }`; none -> `{}`.
  - **authentication** (inverse of M5-28): Basic `{ "type": "basic", "username", "password" }`; Bearer `{ "type": "bearer", "token" }`; ApiKey `{ "type": "apikey", "key", "value", "addTo": "header"|"queryParams" }`; Digest `{ "type": "digest", "username", "password" }`; AwsV4 `{ "type": "iam", "accessKeyId", "secretAccessKey", "sessionToken", "region", "service" }`;
    OAuth2 `{ "type": "oauth2", "grantType": "client_credentials"|"authorization_code"|"password", "accessTokenUrl", "authorizationUrl", "clientId", "clientSecret", "scope", "audience", "username", "password", "redirectUrl", "credentialsInBody": <bool>, "headerPrefix" }`;
    Jwt: no equivalent -> `{}` and `Warning` `Strings.ExportInsomniaAuthUnsupported(path, "jwt")`; None -> `{}`.
- **Templates** (`InsomniaTemplateWriter.Translate(string text, string path, ImportReport report)`, applied to every string value written): `{{name}}` -> `{{ _.name }}` when `name` matches `^[A-Za-z_][A-Za-z0-9_.]*$` (dots are kept: `{{ _.a.b }}`); otherwise `{{ _['name'] }}` (single quotes inside the name are not expected; a name containing `'` is left untouched);
  `{{$guid}}` -> `{% uuid 'v4' %}`; `{{$isoTimestamp}}` -> `{% now 'iso-8601', '' %}`; `{{$timestamp}}` -> `{% now 'unix', '' %}`; `{{$randomInt}}` and any other `{{$...}}` left unchanged with `report.AddOnce(Warning, path, Strings.ExportInsomniaDynamicVariable(name))`.
- **Scripts** are not exported: when any request, folder or the collection has a non-empty script one `Warning` per export `Strings.ExportInsomniaScripts(count)` ("{0} scripts were not exported: Insomnia uses a different scripting API.").
- Strings (EN and FR): `export.insomnia.authUnsupported` = "{0}: authentication type {1} has no Insomnia equivalent and was left out."; `export.insomnia.dynamicVariable` = "The dynamic variable {{{0}}} has no Insomnia equivalent and was left as written."; `export.insomnia.scripts`.

## Out of scope

Cookie jars, Insomnia v5, API specs.

## Acceptance criteria

- [x] Tests comparing exact JSON for a collection with a nested folder, a base environment with enabled and disabled rows, two sub environments, requests with every body kind and every auth kind, disabled param and header; template translation cases (plain, dotted, odd name, each dynamic variable); the JWT and scripts warnings; resource ids and order; trailing `\n`.
- [x] Round trip with `InsomniaImporter` (M5-27 to M5-29): export then import gives equal names, methods, URLs, params, headers, bodies, auth (except Jwt), variables and scoped environments; if those cards are not `done`, leave it to M5-66.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export collections as Insomnia v4`
