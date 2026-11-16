# M4-55: More code generation languages

Status: blocked
Platform: any
Depends on: M4-50

## Open question for the product owner

Which languages and libraries, beyond the six of ADR 0015 (cURL, JavaScript `fetch`, Python `requests`, C# `HttpClient`, PowerShell
`Invoke-RestMethod`, Go `net/http`), must milestone 4 generate?

Candidates, with the card size each would take (one card per generator, like M4-51 to M4-57):

- JavaScript: Node `axios`, XMLHttpRequest, Node `http`.
- Java: `java.net.http.HttpClient`, OkHttp.
- Kotlin (OkHttp), Swift (`URLSession`), PHP (cURL, Guzzle), Ruby (`Net::HTTP`), Rust (`reqwest`), Dart (`http`), R (`httr`), Shell (`wget`, `httpie`).

Recommendation: ship the six of the ADR in M4, and add Java (`java.net.http`), Swift (`URLSession`), PHP (cURL), Ruby (`Net::HTTP`) and Rust (`reqwest`) in a later
milestone only if users ask, because each new generator is a long-term maintenance surface with its own escaping rules.

Why it blocks: choosing the list is a product scope decision. Nothing else depends on it (the registry accepts new generators without changes).

## Goal

(To be written once the question is answered; new cards will be numbered after M4-81.)

## Out of scope

Everything until the answer.

## Suggested commit message

(none yet)
