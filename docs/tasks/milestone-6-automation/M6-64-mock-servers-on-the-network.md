# M6-64: Mock servers reachable from other devices

Status: done
Platform: any
Depends on: M6-34

## Goal

Let a phone, a virtual machine or a teammate reach a Yaapic mock server (ADR 0020 binds `127.0.0.1` only).

## Decision (product owner, 2026-11-27)

**Not now** (option 4). Mock servers stay loopback HTTP only (`127.0.0.1`), as ADR 0020 says. Rejected for now: an opt-in LAN binding, LAN
binding with a self-signed certificate, HTTPS on loopback.

What it means for users:

- A mock server is reachable only from the computer that runs Yaapic, over plain HTTP. Nothing on the network can see the mock data, and
  no firewall prompt appears.
- To reach a mock from a phone, a virtual machine or a teammate, run a tunnel tool of your choice (for example an SSH port forward or a
  tunnelling service) that forwards to `http://127.0.0.1:{port}`. Yaapic does not ship or configure one.
- Apps that require `https://` cannot call a mock directly; a tunnel that terminates TLS works.

Not delivered: binding to `0.0.0.0`/`::` or a chosen interface, HTTPS mocks, showing LAN addresses.

Revisit when: users need device or emulator testing often enough that a tunnel is a burden.

No code and no new card. Docs: `docs/automation.md` ("Mock servers" and "Not delivered"), ADR 0020 points here.

## Acceptance criteria

- [x] Rewritten after the answer; nothing to implement.

## Suggested commit message

`docs: record the decision on mock servers reachable from other devices`
