# M5-47: HAR export

Status: blocked
Platform: any
Depends on: M5-36

## Goal

Export requests as a HAR 1.2 file.

## Open question (product decision, answer before picking this card)

A HAR file describes requests **and responses with timings**. Yaapic stores no response bodies (history keeps status, duration, size and the request only, ADR 0008), so an export can only carry what the history knows. What should "Export HAR" produce?

1. **History export**: the entries of the send history as HAR entries, each with the request as sent, the status code and text, the duration as `time`, `bodySize` from the size, and **empty response content** (`"content": {"size": N, "mimeType": ""}` without text); response headers are missing too. Useful to share "what I called and how it went", not to replay a response.
2. **Collection export** (requests only): every request of a collection as an entry with an empty response (`status: 0`). Most HAR tools reject or mis-render entries without a response.
3. **Store responses first**: add an opt-in "keep response bodies in history" (a storage and privacy decision: bodies may contain secrets and the history is not in Git) and then export complete entries. This is a bigger feature (new history format, size limits, a setting) and would be its own milestone card set.
4. **Do not offer HAR export** (import only, which is what M5 delivers); cURL, Postman and OpenAPI cover sharing.

Until answered, no other card depends on this one.

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on HAR export`
