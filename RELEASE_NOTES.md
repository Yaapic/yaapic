<!-- lang:en -->
## What's new in 0.2.0

- **French and English.** Yaapic follows your system language (French or English) and you can change it in Settings (the gear, or "Settings" in the command palette). The change applies at the next start, with a "Restart now" button.
- **Accent colour.** Pick one of eight colours, the Windows accent (WinUI) or any `#RRGGBB` in Settings; it applies at once, in the dark and the light theme, with readable text on top.
- **Environments per collection.** Tie an environment to a collection ("Available in" in the environments manager): the environment menu and the command palette then offer it only for requests of that collection, plus the environments tied to none. Requests outside any collection get the untied ones. Existing environments stay available everywhere.
- **Autocompletion.** Typing `{{` in the URL or in a header or parameter field lists the variables available for the request (environment, collection, globals, `$guid`...), with their scope and value, plus the variables of the other environments marked "not active". In the script editors, typing `pm.` or `console.` lists the API, for JavaScript and Lua, with chains such as `pm.expect(x).to.be.`. Ctrl+Space opens the list on demand.
- The separate "environments" button is gone: use the environment menu ("Manage environments...") or Ctrl+Shift+E.
- "Updates and about" is now "Settings" and also holds the language and the accent colour.

<!-- lang:fr -->
## Nouveautés de la 0.2.0

- **Français et anglais.** Yaapic suit la langue du système (français ou anglais) et vous pouvez la changer dans Paramètres (l'engrenage, ou « Paramètres » dans la palette de commandes). Le changement s'applique au prochain démarrage, avec un bouton « Redémarrer maintenant ».
- **Couleur d'accentuation.** Choisissez l'une des huit couleurs, la couleur d'accentuation de Windows (WinUI) ou n'importe quel `#RRGGBB` dans Paramètres ; elle s'applique immédiatement, en thème sombre comme en thème clair, avec un texte lisible par-dessus.
- **Environnements par collection.** Associez un environnement à une collection (« Disponible dans » dans le gestionnaire d'environnements) : le menu des environnements et la palette de commandes ne le proposent alors que pour les requêtes de cette collection, en plus des environnements sans association. Les requêtes hors collection obtiennent ceux sans association. Les environnements existants restent disponibles partout.
- **Autocomplétion.** Taper `{{` dans l'URL ou dans un champ d'en-tête ou de paramètre liste les variables disponibles pour la requête (environnement, collection, globales, `$guid`...), avec leur portée et leur valeur, ainsi que les variables des autres environnements marquées « inactif ». Dans les éditeurs de script, taper `pm.` ou `console.` liste l'API, en JavaScript et en Lua, avec les chaînes comme `pm.expect(x).to.be.`. Ctrl+Espace ouvre la liste à la demande.
- Le bouton « environnements » séparé a disparu : utilisez le menu des environnements (« Gérer les environnements... ») ou Ctrl+Maj+E.
- « Mises à jour et à propos » devient « Paramètres » et regroupe aussi la langue et la couleur d'accentuation.
