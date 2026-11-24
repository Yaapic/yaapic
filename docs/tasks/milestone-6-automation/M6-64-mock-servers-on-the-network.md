# M6-64: Mock servers reachable from other devices

Status: blocked
Platform: any
Depends on: M6-34

## Goal

Let a phone, a virtual machine or a teammate reach a Yaapic mock server (ADR 0020 binds `127.0.0.1` only).

## Open question (product decision, answer before picking this card)

1. **Opt-in LAN binding**: a per-server "Allow other devices on my network" switch binds `0.0.0.0`/`::`; the UI shows the LAN addresses and a
   warning. On Windows the first bind triggers the firewall prompt; on macOS the "accept incoming connections" prompt. Mock data is exposed to
   the network while it runs.
2. **Opt-in LAN binding plus HTTPS**: as 1, with a self-signed certificate generated per workspace (clients must trust it; mobile apps often
   refuse self-signed certificates without extra setup).
3. **HTTPS on loopback only**: for apps that require `https://` even locally; no LAN exposure.
4. **Not now**: loopback HTTP only (the current behaviour); use a tunnel tool if needed.

Until answered, no other card depends on this one.

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on mock servers reachable from other devices`
