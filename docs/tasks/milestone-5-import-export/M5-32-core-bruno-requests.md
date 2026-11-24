# M5-32: Core: Bruno import, request files

Status: done
Platform: any
Depends on: M5-06, M5-31

## Goal

One `.bru` request file becomes one `HttpRequestDefinition` (URL, parameters, headers, body, authentication). Collection structure is M5-33, scripts and variables M5-34, environments M5-35.

## Design decisions

- Read first: ADR 0017 (Bruno), M5-31 (`BruFile`, `BruBlock`), M5-06, M5-13 (body shapes), M5-14 (auth targets and defaults), `src/Yaapic.Domain/Http/RequestAuth.cs`.
- `src/Yaapic.Core/Import/Bruno/BrunoRequests.cs` (internal static): `HttpRequestDefinition? Map(BruFile file, string path, Guid id, ImportReport report, out BruAuthMode authMode)` where `enum BruAuthMode { None, Inherit, Mapped }` (the caller resolves inheritance in M5-33). Returns null (with a `Skipped` entry) when the file has no verb block.
- **Verb block**: the first block named `get`, `post`, `put`, `patch`, `delete`, `head`, `options` (a `trace`, `connect` or custom verb: `Skipped` `Strings.ImportMethodUnsupported`). Its pairs: `url`, `body` (mode), `auth` (mode). `meta.name` -> `Name = SafeName(name, DescribeRequest(method, url))`; `meta.type` other than `http` (`graphql` is accepted as HTTP: the body is `body:graphql`; `grpc`, `ws`: `Skipped` `Strings.ImportBrunoTypeUnsupported(path, type)`).
- **URL**: `url` value; `SplitUrl` gives the base and the params in the URL. `params:query` block (when present) is **authoritative**: its pairs (`Enabled` from `~`) replace the query pairs found in the URL. `params:path` pairs build the dictionary for `SubstitutePathVariables` (`:id` in the path).
  `{{process.env.NAME}}` placeholders stay as written and add `AddOnce(Warning, path, Strings.ImportBrunoProcessEnv)`.
- **Headers**: the `headers` block pairs (`Enabled` from `~`), all kept.
- **Body** by the `body` mode of the verb block:

  | Mode | Mapping |
  |---|---|
  | `json` | `Raw Json`, text = `body:json` text |
  | `text` | `Raw Text`, `body:text` |
  | `xml` | `Raw Xml`, `body:xml` |
  | `sparql` | `Raw Text`, `body:sparql`, plus header `Content-Type: application/sparql-query` unless a Content-Type header exists |
  | `graphql` | `Raw Json` with compact JSON `{"query": <body:graphql text>, "variables": <body:graphql:vars text parsed as JSON, or {} when empty or invalid>}`; add `Content-Type: application/json` when absent; `Info` `Strings.ImportBrunoGraphQl` |
  | `formUrlEncoded` | `UrlEncoded` from the `body:form-urlencoded` pairs (`Enabled` from `~`) |
  | `multipartForm` | `FormData` from the `body:multipart-form` pairs: a value of the form `@file(path)` (optionally followed by `@contentType(...)`) is a `File` field with `Value = path` and `Warning` `Strings.ImportFileReference`; others `Text` |
  | `file` | `Binary` from the `body:file` block: the first pair whose value starts `@file(`; its path; `Warning` `Strings.ImportFileReference`; no such pair: none |
  | `none`, missing, other | none (other: `Warning` `Strings.ImportBrunoBodyModeUnsupported(path, mode)`) |

- **Auth** by the `auth` mode of the verb block (missing or `none` -> `None`; `inherit` -> `authMode = Inherit` and `RequestAuth.None` returned for the caller to replace); the parameters come from the block `auth:<mode>`:

  | Mode | Yaapic |
  |---|---|
  | `basic` | Basic: `username`, `password` |
  | `bearer` | Bearer: `token` |
  | `digest` | Digest: `username`, `password` |
  | `awsv4` | AwsV4: `accessKeyId`, `secretAccessKey`, `sessionToken`, `region`, `service` |
  | `apikey` | ApiKey: `key`, `value`, `placement` `queryparams` -> Query else Header |
  | `oauth2` | OAuth2: `grant_type` `client_credentials`/`authorization_code`/`password` (others: `Warning`, no auth); `access_token_url` -> TokenUrl, `authorization_url`, `client_id`, `client_secret`, `scope`, `username`, `password`, `callback_url` -> RedirectUri, `token_placement` `query` -> Query else Header, `token_header_prefix` (default `Bearer`), `token_query_key` (default `access_token`), `credentials_placement` `body` -> `ClientAuthentication.Body` else BasicHeader |
  | `wsse`, `ntlm`, others | `Warning` `Strings.ImportAuthUnsupported(path, mode)`, no auth |

  The shared plain-text credentials `Info` (`Strings.ImportAuthPlainText`) applies as in M5-14.
- `Settings`: defaults. `Scripts`: empty (M5-34). `docs` block: dropped (counted for `Strings.ImportDescriptionsDropped`). `assert` block: `Warning` per import `Strings.ImportBrunoAssertDropped(count)` ("{0} assertion blocks (assert) were not imported.").
- Strings (EN and FR): `import.brunoTypeUnsupported` = "{0}: requests of type \"{1}\" are not supported yet."; `import.bruno.processEnv` = "{{process.env.*}} variables are not available in Yaapic; define them as environment variables instead."; `import.bruno.graphQl` = "A GraphQL body was converted to a JSON body.";
  `import.bruno.bodyModeUnsupported` = "{0}: the body mode \"{1}\" is not supported; the request has no body."; `import.bruno.assertDropped`.

## Out of scope

Collection and folder files, inheritance (M5-33), scripts, variables, environments.

## Acceptance criteria

- [x] Tests (parse a Bru text, then map): the sample request of M5-31 with exact result; each verb; unsupported verb; each body mode with exact body (json, text, xml, sparql with header, graphql with and without vars, form-urlencoded with a disabled pair, multipart with `@file(...)`, file, none, unknown mode);
  `params:query` overriding a URL query; `params:path` substitution and a missing path value; `process.env` warned once; each auth mode of the table with exact `RequestAuth`; `inherit` returns `authMode = Inherit`; `meta.type: grpc` skipped; graphql `meta.type` accepted; the assert and docs entries; name fallback.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Bruno request files`
