# Yaapic — kit d'assets v1.1 (logo provisoire y↵ · couleur Ion)

Toutes les maquettes, le design system et le prototype sont sur le canevas Design « Yaapic — Identité, design system & maquettes ». Ce dossier contient les assets exploitables pour l'app .NET.

## Arborescence

| Dossier | Contenu |
|---|---|
| `logo/` | Symbole y↵ (fond sombre/clair, mono noir/blanc, sur Ion, 16 px), logotype vectorisé (Space Grotesk 700), lockups horizontal et vertical |
| `app-icons/windows/` | `yaapic-app.svg`, `yaapic.ico` (16–256), PNG 16→256 pour MSIX (`Square44x44Logo.targetsize-*`) |
| `app-icons/macos/` | Squircle 1024 SVG + PNG 16→1024 pour `AppIcon.iconset` (`iconutil -c icns`) |
| `app-icons/linux/` | `dev.yaapic.Yaapic.svg` (hicolor/scalable) + `-symbolic.svg` |
| `app-icons/web/` | `favicon.svg` (suit `prefers-color-scheme`), `favicon.ico`, avatar social 400 |
| `icons/16, 20, 24/` | 46 icônes « Yaapic Line », un SVG par taille, `currentColor`, trait compensé (1,5 px physique) |
| `icons/yaapic-icons-sprite.svg` | Sprite `<symbol id="yi-…">` pour la doc web |
| `tokens/yaapic.tokens.json` | Tokens DTCG / Style Dictionary 4 : palette 50–950 (signature Ion #1FC7D4), sémantique (sombre par défaut, clair et contraste élevé en `$extensions`), typo, espaces, rayons, élévations, tailles, mouvement |
| `xaml/` | `Yaapic.Colors.WinUI.xaml` (ThemeDictionaries Default/Light/HighContrast), `Yaapic.Colors.Avalonia.axaml` (Dark/Light), dictionnaires de géométries d'icônes |

## Conventions de nommage

- Tokens : `catégorie.rôle.variante.état` → `color.accent.fillHover`, `space.300`, `radius.control`.
- Clés XAML : PascalCase préfixé `Yaapic` + suffixe de type → `YaapicAccentFillBrush`, `YaapicRadiusControl`, `YiGrpc` (icônes).
- Fichiers : `kebab-case`, taille ou fond en suffixe → `yaapic-symbol-16-hinted.svg`, `yaapic-lockup-horizontal-light-bg.svg`.

## À valider (voir aussi la note de DA sur le canevas)

1. Back-end pour les workspaces « équipe » : Git seul ou service Yaapic Sync.
2. Fournisseur et mode (local/cloud) de l'assistant IA — désactivé par défaut dans les maquettes.
3. HTTP/3 selon la disponibilité de QUIC par OS (option grisée sinon).
4. Dépôt de la marque « Yaapic » et des noms de domaine.
5. Le logotype est vectorisé depuis Space Grotesk (OFL) : un ajustement typographique à la main reste recommandé avant le dépôt de marque.
