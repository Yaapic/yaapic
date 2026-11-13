## What's new in 0.2.0

- **French and English.** Yaapic follows your system language (French or English) and you can change it in Settings (the gear, or "Settings" in the command palette). The change applies at the next start, with a "Restart now" button.
- **Accent colour.** Pick one of eight colours, the Windows accent (WinUI) or any `#RRGGBB` in Settings; it applies at once, in the dark and the light theme, with readable text on top.
- **Environments per collection.** Tie an environment to a collection ("Available in" in the environments manager): the environment menu and the command palette then offer it only for requests of that collection, plus the environments tied to none. Requests outside any collection get the untied ones. Existing environments stay available everywhere.
- **Autocompletion.** Typing `{{` in the URL or in a header or parameter field lists the variables available for the request (environment, collection, globals, `$guid`...), with their scope and value, plus the variables of the other environments marked "not active". In the script editors, typing `pm.` or `console.` lists the API, for JavaScript and Lua, with chains such as `pm.expect(x).to.be.`. Ctrl+Space opens the list on demand.
- The separate "environments" button is gone: use the environment menu ("Manage environments...") or Ctrl+Shift+E.
- "Updates and about" is now "Settings" and also holds the language and the accent colour.
