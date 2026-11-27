<!-- lang:en -->
## What's new in 0.4.0

- **Collection Runner.** Run every request of a folder in order, as many times as you want, with a delay between requests and an option to stop on the first failure. Pick a CSV or JSON data file to run one iteration per row: each column becomes a variable you can use as `{{column}}` in URLs, headers and bodies. Results show the summary, each iteration and each request with its tests and console, and can be exported as a JSON or JUnit XML report for your CI. Secret values never reach a report.
- **Load tests.** Have 1 to 100 virtual users send a folder's requests for 10 seconds to an hour, with a fixed or ramp-up profile and an optional think time or data file. Live tiles and four charts (requests per second, average response time, errors per second, active users) show how it goes; percentiles (p50 to p99) and throughput are available overall and per request, and a JSON report is saved at the end. Yaapic asks you to confirm the target hosts before the first test.
- **Mock servers.** Answer requests locally with canned responses: routes (method, path with `:name` parameters and `*`, status, headers, body, delay) are stored in your workspace in `mocks/` and shared through Git. Response bodies can use templates (`{{params.id}}`, `{{query.name}}`, `{{$guid}}`...), CORS is on by default, and a request log shows what was received and which route answered. "Create a mock server..." on a folder generates one route per request.
- **Monitors.** Run a folder on a schedule (every 5 minutes to once a day) against a fixed environment. Each monitor keeps its run history, and the status bar shows how many monitors are failing.
- **Automation views in the main window.** Runner, Load test, Mock servers and Monitors have their own buttons on the left rail and open in the main window instead of the request tabs. They are also reachable from a folder's context menu, the command palette and the status bar, and keep working while you look at something else. Collections, History or opening a request brings your tabs back as you left them.
- **Scripting for the Runner.** `yaapic.iterationData` reads the current row, `yaapic.info.iteration` gives the iteration number, `yaapic.execution.setNextRequest(...)` jumps to another request (or stops the run with `null`) and `yaapic.execution.skipRequest()` skips the current request from a pre-request script. Available in JavaScript and Lua, with completion and snippets.

### Known limits

- Monitors and mock servers only run while Yaapic is open; nothing runs in the background once it is closed.
- Mock servers answer on `127.0.0.1` only, over HTTP: other devices on your network cannot reach them.
- There is no command-line runner yet: reports are exported from the Runner window.
- The automation views were built and tested on Windows only. The macOS and Linux builds have not been run yet, so expect rough edges there.
- At high display scaling (for example 150 %), the minimum window size becomes too small and some automation views are clipped when the window is at its minimum: enlarge the window.

<!-- lang:fr -->
## Nouveautés de la 0.4.0

- **Runner de collection.** Exécutez toutes les requêtes d'un dossier dans l'ordre, autant de fois que vous le souhaitez, avec un délai entre les requêtes et l'option de s'arrêter au premier échec. Choisissez un fichier de données CSV ou JSON pour lancer une itération par ligne : chaque colonne devient une variable utilisable en `{{colonne}}` dans les URL, les en-têtes et les corps. Les résultats montrent le résumé, chaque itération et chaque requête avec ses tests et sa console, et s'exportent en rapport JSON ou JUnit XML pour votre CI. Les valeurs secrètes n'atteignent jamais un rapport.
- **Tests de charge.** Faites envoyer par 1 à 100 utilisateurs virtuels les requêtes d'un dossier pendant 10 secondes à une heure, avec un profil fixe ou à montée en charge, et en option un temps de réflexion ou un fichier de données. Des indicateurs en direct et quatre graphiques (requêtes par seconde, temps de réponse moyen, erreurs par seconde, utilisateurs actifs) montrent le déroulement ; les percentiles (p50 à p99) et le débit sont disponibles globalement et par requête, et un rapport JSON est enregistré à la fin. Yaapic vous demande de confirmer les hôtes ciblés avant le premier test.
- **Serveurs de mock.** Répondez localement aux requêtes avec des réponses prédéfinies : les routes (méthode, chemin avec paramètres `:nom` et `*`, statut, en-têtes, corps, délai) sont stockées dans votre espace de travail, dans `mocks/`, et partagées via Git. Les corps de réponse acceptent des modèles (`{{params.id}}`, `{{query.nom}}`, `{{$guid}}`...), CORS est activé par défaut, et un journal indique ce qui a été reçu et quelle route a répondu. « Créer un serveur de mock... » sur un dossier génère une route par requête.
- **Moniteurs.** Exécutez un dossier selon un calendrier (de toutes les 5 minutes à une fois par jour) avec un environnement fixe. Chaque moniteur conserve son historique d'exécutions, et la barre d'état indique combien de moniteurs échouent.
- **Vues d'automatisation dans la fenêtre principale.** Le Runner, les tests de charge, les serveurs de mock et les moniteurs ont leurs propres boutons dans la barre latérale gauche et s'ouvrent dans la fenêtre principale à la place des onglets de requêtes. Ils sont aussi accessibles depuis le menu contextuel d'un dossier, la palette de commandes et la barre d'état, et continuent de fonctionner pendant que vous regardez autre chose. Collections, Historique ou l'ouverture d'une requête ramènent vos onglets tels que vous les aviez laissés.
- **Scripts pour le Runner.** `yaapic.iterationData` lit la ligne courante, `yaapic.info.iteration` donne le numéro d'itération, `yaapic.execution.setNextRequest(...)` saute vers une autre requête (ou arrête l'exécution avec `null`) et `yaapic.execution.skipRequest()` ignore la requête courante depuis un script de pré-requête. Disponible en JavaScript et en Lua, avec autocomplétion et extraits.

### Limites connues

- Les moniteurs et les serveurs de mock ne fonctionnent que tant que Yaapic est ouvert ; rien ne tourne en arrière-plan une fois l'application fermée.
- Les serveurs de mock répondent uniquement sur `127.0.0.1`, en HTTP : les autres appareils de votre réseau ne peuvent pas les joindre.
- Il n'y a pas encore de runner en ligne de commande : les rapports s'exportent depuis la fenêtre du Runner.
- Les vues d'automatisation ont été développées et testées sous Windows uniquement. Les versions macOS et Linux n'ont pas encore été lancées : attendez-vous à des défauts.
- Avec une mise à l'échelle d'affichage élevée (par exemple 150 %), la taille minimale de la fenêtre devient trop petite et certaines vues d'automatisation sont tronquées quand la fenêtre est au minimum : agrandissez-la.
