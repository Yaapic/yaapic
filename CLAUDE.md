# Yaapic agent guidelines

## Language and style

- Code, comments, identifiers, READMEs, ADRs and docs are written in English.
- Use C# 15 (`LangVersion` set accordingly in `Directory.Build.props`) and current C# best practices: nullable
  reference types, `required`/`init`, records where they fit, file-scoped namespaces, no reflection (Native AOT).
- Every public type and member carries an XML doc comment (`<summary>`, plus `<param>`, `<returns>`, `<exception>`
  where relevant). A missing public doc comment is a build error (`GenerateDocumentationFile` with CS1591 as error).

## Workflow

- Task cards live in `docs/tasks/`; the `/next-task` skill implements one card per session.
- Commits are GPG-signed with the key of adrien.granger@outlook.com, carry no trailer, and every commit is pushed.
