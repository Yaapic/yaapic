# P-07: Accent colour derivation

Status: todo
Platform: any
Depends on: P-02

## Goal

A pure function that turns one accent colour into the full set of accent tokens for the dark and the light theme.

## Design decisions

- Input `#RRGGBB`; output the keys the token file defines for the accent (`YaapicAccentFillBrush`, hover, pressed, subtle background,
  accent text, text on accent), per theme.
- Hover and pressed by lightness shifts; the text on the accent is black or white, whichever contrasts more; the accent text colour is
  nudged until it reaches 4.5:1 on the theme background.
- The preset palette (teal, blue, purple, pink, red, orange, green, grey) is data; teal must reproduce today's tokens exactly.

## Acceptance criteria

- [ ] The teal output equals the current token file values.
- [ ] Contrast tests for every preset in both themes.

## Suggested commit message

`feat: derive the accent tokens from one colour`
