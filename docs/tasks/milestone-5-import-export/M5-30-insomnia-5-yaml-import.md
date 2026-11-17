# M5-30: Insomnia 5 YAML import

Status: blocked
Platform: any
Depends on: M5-07, M5-27

## Goal

Import the YAML format that Insomnia 5 and later write (`type: collection.insomnia.rest/5.0`).

## Open question (product decision, answer before picking this card)

Insomnia 5 changed its export to a nested YAML document (`collection:` tree with `children`, `environments:`, `meta:` blocks) that is not documented as a stable interchange format, and it differs between 5.x releases. Should Yaapic:

1. support it **now**, written from sample exports the user provides (the card is then rewritten with the field mapping, as M5-27 to M5-29, on top of `VYamlTreeReader` and the existing Insomnia mapping code); or
2. **not support it**, and tell Insomnia 5 users to export "Insomnia v4 (JSON)" (still offered by Insomnia's export dialog), with the importer recognising the v5 header and showing that message; or
3. wait for the format to stabilise.

Until answered, no other card depends on this one. The Insomnia importer (M5-27 to M5-29) must, in any case, recognise a v5 file in `Sniff` (`type: collection.insomnia.rest/5` at the start) so the message of option 2 can be shown: that recognition is **not** part of this card; if option 2 is chosen,
the new card adds `Strings.ImportInsomnia5Unsupported` and a `Possible` sniff plus an `ImportException`.

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on Insomnia 5 import`
