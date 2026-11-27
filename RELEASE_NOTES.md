<!-- lang:en -->
## What's new in 0.4.1

A fix release for the automation features of 0.4.0.

- **Pick the folder to run.** Opening the Runner or Load test from the left rail, the status bar or the command palette used to show an empty request list. Both pages now ask you to choose a folder (the choice is also offered when you opened them from a folder's context menu, which still pre-selects that folder), and Run and Start stay disabled until you do. The request list keeps a usable height instead of collapsing in small windows.
- **Resizable panes, remembered.** The sidebar (Requests and History) and the request lists of the Runner and Load test can be resized by dragging their separator or with the left and right arrow keys; double-click a separator to restore its default width. Yaapic remembers the widths between launches.
- **Load tests against a mock server.** Concurrent requests could corrupt the mock server's request log, so the mock answered with errors under load. Fixed.
- **Monitors.** Reloading the monitor list while a refresh was in progress could show a monitor twice, and the workspace file stores could be read from several threads at once. Both fixed, and a monitor's run now appears in its history only after its warning has been written to the console.
- **Settings.** Changing the language or the accent colour no longer resets other saved preferences.

### Known limits

- Monitors and mock servers only run while Yaapic is open; nothing runs in the background once it is closed.
- Mock servers answer on `127.0.0.1` only, over HTTP: other devices on your network cannot reach them.
- There is no command-line runner yet: reports are exported from the Runner page.
- The macOS and Linux builds of the automation views have been built and tested on Windows only and have not been run on those systems yet, so expect rough edges there.
- At high display scaling (for example 150 %), the minimum window size is smaller than it should be and some automation views are clipped when the window is at its minimum: enlarge the window.

<!-- lang:fr -->
## Nouveautés de la 0.4.1

Une version corrective pour les fonctions d'automatisation de la 0.4.0.

- **Choisir le dossier à exécuter.** Ouvrir le Runner ou les tests de charge depuis la barre latérale gauche, la barre d'état ou la palette de commandes affichait une liste de requêtes vide. Les deux pages vous demandent maintenant de choisir un dossier (le choix reste proposé quand vous les avez ouvertes depuis le menu contextuel d'un dossier, qui présélectionne toujours ce dossier), et Exécuter et Démarrer restent désactivés tant que vous n'en avez pas choisi un. La liste des requêtes garde une hauteur utilisable au lieu de s'écraser dans les petites fenêtres.
- **Panneaux redimensionnables, mémorisés.** La barre latérale (Requêtes et Historique) et les listes de requêtes du Runner et des tests de charge se redimensionnent en glissant leur séparateur ou avec les flèches gauche et droite ; un double-clic sur un séparateur rétablit sa largeur par défaut. Yaapic mémorise les largeurs d'un lancement à l'autre.
- **Tests de charge contre un serveur de mock.** Des requêtes simultanées pouvaient corrompre le journal du serveur de mock, qui répondait alors par des erreurs sous charge. Corrigé.
- **Moniteurs.** Recharger la liste des moniteurs pendant qu'un rafraîchissement était en cours pouvait afficher un moniteur en double, et les fichiers de l'espace de travail pouvaient être lus par plusieurs threads à la fois. Les deux sont corrigés, et l'exécution d'un moniteur n'apparaît dans son historique qu'une fois son avertissement écrit dans la console.
- **Réglages.** Changer la langue ou la couleur d'accent ne réinitialise plus les autres préférences enregistrées.

### Limites connues

- Les moniteurs et les serveurs de mock ne fonctionnent que tant que Yaapic est ouvert ; rien ne tourne en arrière-plan une fois l'application fermée.
- Les serveurs de mock répondent uniquement sur `127.0.0.1`, en HTTP : les autres appareils de votre réseau ne peuvent pas les joindre.
- Il n'y a pas encore de runner en ligne de commande : les rapports s'exportent depuis la page du Runner.
- Les versions macOS et Linux des vues d'automatisation ont été construites et testées sous Windows uniquement et n'ont pas encore été lancées sur ces systèmes : attendez-vous à des défauts.
- Avec une mise à l'échelle d'affichage élevée (par exemple 150 %), la taille minimale de la fenêtre est plus petite qu'elle ne devrait l'être et certaines vues d'automatisation sont tronquées quand la fenêtre est au minimum : agrandissez-la.
