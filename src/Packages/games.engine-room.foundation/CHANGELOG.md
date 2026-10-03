# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `Bootstrap.BootCoverView`: an opaque overlay canvas (`BootCover`, sort order 32000) in the root prefab, registered by `RootLifetimeScope`.
- `LoadingScreen.WaitForViewAsync`: completes once a Loading view is attached (at once if one already is); throws `OperationCanceledException` when the screen is disposed first.
- Cheat console (Editor and development builds only, `UNITY_EDITOR || DEVELOPMENT_BUILD`): a `CheatConsole` overlay canvas (sort order 30000) in the root prefab with `Core.Cheats.CheatConsoleView`, registered by `RootLifetimeScope`. Backquote toggles it; while typing, a suggestion list attached under the input shows the matching names and values: Up/Down move through it (or recall history when no list is shown), Tab or Enter accept the highlighted one, Tab without a highlight autocompletes, Escape hides the list first and closes the console when none is shown; built-in `help [cheat]` and `clear`. Cheats implement `ICheat` or `ICheatProvider` (`CheatCommand`), declare `CheatParameter`s and are registered per scope with `builder.RegisterCheat<T>()` / `builder.RegisterCheats<T>()`. Agents call `CheatConsoleView.Submit(line)` and read `LastReply`; `Submitted` carries those lines, `Entered` the lines typed and confirmed with Enter. Release builds deactivate the console object.
- `GameInput` gains a `Debug` map (`ToggleConsole`, `CloseConsole`, `CompleteCommand`, `PreviousCommand`, `NextCommand`); `InputService` keeps it enabled in Editor and development builds, outside the `InputMaps` stack.
- `Core.asmdef` references `R3.Unity` and `UnityEngine.UI` (both already required by the package).

### Fixed

- The first frames after launch showed the empty boot scene (the root camera's skybox) until the Loading scene had loaded. The boot cover now covers them from the first frame; `GameFlow` hides it in the frame the Loading view attaches, already visible. Debug-domain and PlayMode test boots hide it at once.
- A domain run with `Transition.Loading` revealed the screen before the sub-domains it starts during startup had loaded (empty frames between the reveal and the sub-domain's scene). `DomainRunner` now counts a domain's readiness as including those sub-domains: the loading screen also waits for their scope scenes, content loads and first `Start`. Sub-domains started after the reveal are unaffected.

## [1.1.0] - 2026-10-02

### Added

- `GameModule.IsUiNavigationEnabled` (off by default): turns on keyboard/gamepad UI navigation on the root EventSystem. `RootLifetimeScope` applies it at boot.
- Game-owned boot scene: **Tools > Foundation > Create Game Module** also creates an empty `Assets/_Project/<Game>/Scenes/Boot.unity` and puts it first in Build Settings, replacing the package's `Bootstrap.unity`, which a game consuming the package from git cannot open.
- **Tools > Foundation > Create Boot Scene** (`Bootstrap.Editor.GameBootScene.CreateForActiveModule`) migrates an existing game: it creates `Scenes/Boot.unity` next to the active game module's `VContainerSettings` and swaps the Build Settings entry. Safe to run again.
- A `Bootstrap.Tests` EditMode test fails when the Build Settings boot scene holds GameObjects, and player builds warn about it: app-lifetime objects belong in the root prefab.
- `BootstrapScene.PackagePath`, `FindBootPath`, `PlaceFirst`, `CountGameObjectsAt` and `CountGameObjects`.

### Changed

- The root prefab's EventSystem no longer sends navigation events (Move/Submit/Cancel) by default, so Enter, Space or gamepad South no longer re-click the last clicked uGUI button. Games that use keyboard or gamepad UI navigation tick `IsUiNavigationEnabled` on their game module asset.
- Debug runs from a scope scene boot through the first enabled Build Settings scene and fall back to the package's `Bootstrap.unity` when none is listed (they logged an error before). The package scene is kept only as that fallback.

### Migration

- Games set up on 1.0.0 keep working unchanged. To own the boot scene, run **Tools > Foundation > Create Boot Scene** once and commit the new scene and `ProjectSettings/EditorBuildSettings.asset`.

## [1.0.0] - 2026-10-01

### Added

- First release as a package, extracted from the Unity project template: Core (domain runner, scopes, save store, settings, input, audio, localization, content directories, logging), Shared (UI widgets, TestUtils), Bootstrap (root prefab, default `VContainerSettings`, `Bootstrap.unity`, play-from-any-scene), and the Loading and Settings domains, with their tests.
- Editor tools: `Tools/Foundation/Create Game Module`, `Tools/Foundation/Use Package From Git`, `Tools/Input/Generate GameInput`, `Tools/Localization/Generate Text Keys`, `Build/Content Directories`.
