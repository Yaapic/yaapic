# M4-37: Server certificate verification: skip or custom CA

Status: blocked
Platform: any
Depends on: M4-34

## Open question for the product owner

How should Yaapic let users call servers whose TLS certificate the system does not trust (self-signed development servers, a company CA)?

Options:

1. **Per-request "Verify TLS certificates" toggle** in the request Settings tab (default on), saved in the request YAML
   (`settings.verifyTls: false`), with a visible warning badge on the request tab and in the response when off (recommended: it is what
   Postman and Insomnia do and it is the least surprising for a shared collection, but a collection committed to Git can then carry a
   disabled check: the badge makes it visible). About 5 cards (domain and YAML, transport, view model, two UIs).
2. **Global setting only** (Settings window): "Do not verify certificates for these hosts" (a host-pattern list like the client
   certificates, never in the workspace). Safer for shared collections, slower to use. About 6 cards.
3. **Custom CA bundle** (a PEM file added to the trust used by Yaapic) instead of disabling verification: the safest, the least convenient for
   quick tests. About 5 cards, can be combined with option 2.
4. **Neither** in M4.

Why it blocks: it is a security posture decision, and option 1 changes the shared file format and the request Settings tab on both
front-ends. Nothing else in M4 depends on it.

## Goal

(To be written once the question is answered; the cards will be numbered after M4-81.)

## Out of scope

Everything until the answer.

## Suggested commit message

(none yet)
