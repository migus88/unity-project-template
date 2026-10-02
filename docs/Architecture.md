# Architecture

The ideas behind the template, in plain words, with a few examples to map each concept to the code. Binding rules are in `docs/Rules.md`; detailed, code-linked guidance (which files show which pattern) lives in the project skills, see "Finding the code" below.

## Mental model

- The game is a tree of **domains**. A domain is a feature with its own lifetime: it starts, owns some scenes, UI and state while it runs, and ends with a **result**.
- Launching a domain works like calling an async function: the launcher passes arguments, awaits the domain, and handles the result it returns. That result is the only way domains communicate; there is no message bus and no shared global state.
- Everything that lives for the whole app (running domains, saving, settings, input, audio, time, localization, content loading, logging) is infrastructure in **Core**.
- **Bootstrap** is the game-agnostic start of the app. The game itself plugs in as a **game module**, which provides the **main flow**: the top-level order of screens and modes.
- Removing a feature means deleting its domain folder; only the few places that launch it break, and the compiler finds them.

## Layers and dependencies

First-party code comes in five kinds of modules. Core, Shared, Bootstrap and the Loading and Settings domains form the foundation package (see below); the game module and its domains live in `src/Assets/_Project/`:

- **Core**: app-lifetime infrastructure, e.g. the domain runner, save store, settings service, input service. Depends on nothing first-party.
- **Shared**: reusable view widgets (`Shared.UI`, e.g. `SelectorView`) and test helpers (`TestUtils`). Depends on Core only.
- **Domains**: features with a lifetime, one folder each under `Domains/`.
- **Bootstrap**: the composition root. Builds the root scope (`RootLifetimeScope`), boots the app (`GameFlow`), hands over to the main flow. Knows Core, Shared and leaf domains, never the game's main domains.
- **Game module**: the actual game, e.g. the `Sample` module. Registers the main domains and the main flow. Nothing depends on it.

```
Game module ──► Bootstrap ──► leaf domains ──► Shared ──► Core
     └────────► main domains ──► leaf domains
```

Dependencies point one way, towards Core. Because only the game module knows the main domains, replacing or removing the game never touches Bootstrap. When two layers must cooperate against this direction, Core declares a small contract, a domain fulfils it and Bootstrap wires the two together. Example: Core's `ILoadingScreen` (with a `NullLoadingScreen` default), implemented by the Loading domain (Rules §1).

## Domains

There are three kinds:

- **Main domain**: a top-level mode of the game, e.g. MainMenu or Gameplay. Registered by the game module and launched by the main flow.
- **Leaf domain**: a reusable, self-contained feature that any domain may launch, e.g. Settings or Loading.
- **Sub-domain**: a part of a main domain with its own lifetime that shares the parent's code and services, e.g. Gameplay's Pause.

Nesting is at most three levels: root, then main, then sub or leaf.

Every domain has the same small public face, named after it (Settings shown):

- an **entry class** (`SettingsDomain`) with `RunAsync(args, transition, ct)`, which hands the work to Core's `DomainRunner`;
- an **arguments record** (`SettingsArgs`) and a **result union** (`SettingsResult`), a closed set of cases;
- a **descriptor** asset (`SettingsDomainDescriptor`) that says how to load it, and a **scope** (`SettingsLifetimeScope`, a `DomainLifetimeScope`) in its scope scene.

Settings is the smallest complete domain and the best one to read first.

### Lifecycle of a run

1. **Start**: a launcher (the main flow or a presenter inside another domain) asks to run the domain with its arguments and a transition (`Transition.Loading` shows the loading screen, `Transition.None` does not).
2. **Load**: the domain's scope scene is loaded and its scope is built as a child of the launcher's scope. The loading screen covers this when requested, including sub-domains the domain starts right away.
3. **Run**: the domain's presenters and services do their work; the player interacts with its views.
4. **Complete**: exactly one flow presenter ends the domain with a result through `DomainCompletion<TResult>`. Because the result is a closed set of cases, the launcher must handle each one.
5. **Teardown**: the scope is disposed and the domain's scenes are unloaded. This happens always: on completion, on failure and on cancellation.

In code, the two ends of a run look like this (from Settings and the sample main flow):

```csharp
_completion.Complete(new SettingsResult.Closed());

var result = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Loading, ct);
var shouldQuit = result.Match(play => false, quit => true);
```

A domain may run alongside others for a long time (Loading runs for the whole session), and a domain may launch its own leaf and sub-domains, which follow the same lifecycle one level deeper (e.g. the main menu opens Settings).

## Boot and the main flow

1. Unity starts with a single, empty boot scene owned by the game module (the package's own bootstrap scene is the fallback before a game module exists). Before it loads, the DI framework creates the **root scope** from a prefab.
2. The root scope installs Core (`CoreInstaller`), registers the always-present leaf domains (Loading, Settings) and installs the game module. A game module is a `GameModule` asset that registers the main domains and an `IMainFlow`; with none assigned the app boots into `IdleMainFlow`, which does nothing.
3. The boot sequence starts the loading screen, initialises settings and the save slot, then awaits the main flow. When the main flow finishes, the app quits.
4. The main flow is the game's script: run the menu, look at its result, run gameplay, and so on. Example: `SampleMainFlow` in the `Sample` module.

In the Editor, pressing Play in any domain's scope scene still boots through the boot scene and then runs just that domain with debug arguments, so every domain can be tried in isolation.

## Scopes (dependency injection)

Scopes form a tree that mirrors the domain tree:

```
Root scope          Core services, domain launchers, the main flow
 ├─ Loading         runs for the whole session
 ├─ Main domain     e.g. the menu
 │   └─ Settings    leaf launched by the menu
 └─ Main domain     e.g. gameplay
     ├─ Pause       sub-domain
     └─ Settings    the same leaf, launched from here too
```

A child scope sees everything registered in its ancestors, never its siblings. A domain's scope exists only while the domain runs, so everything it owns is created at start and disposed at teardown. Plain C# classes receive their dependencies through constructors; scene objects enter a scope as authored instances.

## Presentation

Inside a domain, UI and behaviour follow **Model-View-Presenter**:

- **Views** are passive scene components (e.g. `SettingsView`). They show things and report what the player did; they hold no rules.
- **Presenters** are plain classes (e.g. `SettingsPresenter`) that subscribe to views, apply the rules and update the views. One flow presenter decides when the domain is over.
- **Models and services** hold the domain's state and logic; observable state is exposed read-only.

Input comes in through the generated input callbacks and is routed through a stack of active input maps plus explicit locks. Nothing is spawned at runtime: every object is authored in a scene or prefab and shown or hidden as needed (Rules §3).

## Content and scenes

- Each domain has exactly one **scope scene**, which holds its scope and its views (e.g. the Settings scene), plus optional **content scenes** (rooms, levels, environments) that carry no logic.
- Each domain's loadable scenes and heavy assets belong to its own **content directory** (Unity 6 Content Directories), rooted in its content asset (e.g. `SettingsContent`). In the Editor they load straight from the project; a player build loads them from built content, which is produced by a build step before the player build.
- Only the boot scene is in the build's scene list; everything else is reached through content directories.

## Data

- **Configs** are read-only assets owned by the domain that uses them (e.g. `GameplayConfig`).
- **Save data** is one file per slot, split into sections (`SaveSection<T>`), each owned by one domain. **Settings** are one file with the Core settings plus sections owned by domains (`SettingsSection<T>`).
- Every persisted section is a small versioned data record, serialised to JSON. Changing its shape means bumping its version and adding a migration, so older files keep loading.
- **Localized text** lives in per-domain tables with generated keys.
- Expected failures (a missing or corrupted file, a newer data version) are returned as results rather than thrown; bugs throw.

## The removable example

The template ships a small example game: the `Sample` game module with its main flow, the MainMenu and Gameplay main domains (with the Pause sub-domain). It shows content scenes, input, save and settings sections, and domain-to-domain launching. An Editor menu command removes it.

What remains after removal is the foundation package alone. The app still boots, shows the loading screen and settles into the idle main flow. A real game then adds its own game module, main flow and main domains in the same way the example did. After removal, the example names in this document (MainMenu, Gameplay, Pause, `Sample`) describe the pattern only.

## Foundation package

The game-agnostic modules ship as one UPM package, `games.engine-room.foundation` (`src/Packages/games.engine-room.foundation/`). In the template it is embedded, so the template is where the package is developed; a game references it by git URL and a version tag, and keeps only its own game module and domains in `Assets/`. The game plugs in without editing the package: its root prefab variant sets the `GameModule`, and its own `VContainerSettings` asset makes that variant the root. The package changes through releases (semver, CHANGELOG); its README describes requirements and the update, contribute and release workflow.

## Finding the code

Examples here illustrate; they do not enumerate. To get from a concept to the code:

- Search for the concept: the `LSP` tool's `workspaceSymbol` (for example "Domain", "LifetimeScope", "MainFlow", "Presenter"), then `goToDefinition`, `findReferences` and `goToImplementation`; Grep for non-C# assets and text.
- Load the project skills in `.claude/skills/`, starting with `architecture-map`. They are the code-linked layer: they cite canonical files by path and point to worked examples, and `.claude/skills/check-skills.sh` verifies that every cited path still exists.
- When in doubt, copy the nearest existing domain or Core service.
