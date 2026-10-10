# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Analytics (`Core.Analytics`): code tracks `readonly record struct` events implementing `IAnalyticsEvent` (snake_case `Name`, `Write(IAnalyticsWriter)` with string/int/float/bool parameters) through `IAnalytics.Track<TEvent>` (generic over structs: no boxing). `CoreInstaller` registers `IAnalytics` through `AnalyticsGate.Create`: the scripting define `ANALYTICS_ENABLED` forwards events to `IAnalyticsBackend`, otherwise `NullAnalytics` drops them. `IAnalyticsBackend` is the seam a real backend implements in the game module; `RootLifetimeScope` registers `DummyAnalyticsBackend` (logs `[Analytics] name {key=value, ...}` in Editor and development builds) when the game registers none. Foundation events: `cheat_used` (`Core.Cheats.CheatUsedEvent`, command name of every known cheat submitted to the console, no arguments) and `settings_changed` (`Core.Settings.SettingsChangedEvent`, one per field changed during a Settings visit, tracked on close; `Core.Settings.SettingsChanges.Diff` reports key bindings as `changed`, never their JSON). `TestUtils.FakeAnalytics` records tracked events for tests. `CheatConsolePresenter` and `SettingsPresenter` take an `IAnalytics`.
- Byte files: `IFileStorage.ReadBytesAsync` and `WriteBytesAsync` read and write raw bytes with the same `NotFound`/`Error` mapping and temp-file replace as the text methods (`InMemoryFileStorage` keeps them in `BinaryFiles`). Implementers of `IFileStorage` must add both.
- `Core.Compression.Deflate`: an edge adapter over raw deflate. `Compress(byte[])` uses `CompressionLevel.Optimal`; `Decompress(byte[], int maxLength)` returns `Corrupted` for invalid data (`InvalidDataException`, or the `IOException` Mono throws) or when the inflated size would exceed `maxLength`, so untrusted input cannot inflate without limit.
- Development-only domains: `DomainDescriptor.IsDevelopmentOnly` (off by default). `ContentDirectoryBuilder` leaves their content directories out unless the build is a development build: `BuildAll(bool isDevelopmentBuild)` takes it explicitly, `BuildAll()` and menu `Build/Content Directories` follow the active Development Build setting (`EditorUserBuildSettings.development`), player builds through the Build Player window follow `BuildOptions.Development`. Descriptor validation (names, `EditorContent`) still covers them in release builds. Register and launch such domains under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `Bootstrap.BootCoverView`: an opaque overlay canvas (`BootCover`, sort order 32000) in the root prefab, registered by `RootLifetimeScope`.
- `LoadingScreen.WaitForViewAsync`: completes once a Loading view is attached (at once if one already is); throws `OperationCanceledException` when the screen is disposed first.
- Cheat console (Editor and development builds only, `UNITY_EDITOR || DEVELOPMENT_BUILD`): a `CheatConsole` overlay canvas (sort order 30000) in the root prefab with `Core.Cheats.CheatConsoleView`, registered by `RootLifetimeScope`. Backquote toggles it; while typing, a suggestion list attached under the input shows the matching names and values: Up/Down move through it (or recall history when no list is shown), Tab or Enter accept the highlighted one, Tab without a highlight autocompletes, Escape hides the list first and closes the console when none is shown; built-in `help [cheat]` and `clear`. Cheats implement `ICheat` or `ICheatProvider` (`CheatCommand`), declare `CheatParameter`s and are registered per scope with `builder.RegisterCheat<T>()` / `builder.RegisterCheats<T>()`. Agents call `CheatConsoleView.Submit(line)` and read `LastReply`; `Submitted` carries those lines, `Entered` the lines typed and confirmed with Enter. Release builds deactivate the console object.
- Looping SFX: `IAudioService.PlayLoop(AudioCue)` plays a 2D loop on one of the authored SFX sources and returns a `Core.Audio.AudioLoop` handle (`IsPlaying`, `Stop`, `Dispose`). `default(AudioLoop)` is a silent handle; `Stop`/`Dispose` are idempotent and never touch a newer sound on the same source; the service's `Dispose` stops every loop. The cue must have `Loop` set (`PlayLoop` throws otherwise, as `Play` still throws on a looping cue) and plays through its mixer group, so it follows the SFX and Master volumes. Loops are never reclaimed as finished; when every source is busy, the oldest one-shot is stolen first and a loop only when all sources hold loops (that loop's handle goes silent). Implementers of `IAudioService` must add `PlayLoop`.
- `GameInput` gains a `Debug` map (`ToggleConsole`, `CloseConsole`, `CompleteCommand`, `PreviousCommand`, `NextCommand`); `InputService` keeps it enabled in Editor and development builds, outside the `InputMaps` stack.
- `Core.asmdef` references `R3.Unity` and `UnityEngine.UI` (both already required by the package).
- UI interaction sounds: `Core.Audio.UiInteraction` (Hover, Click, Back, Step, Tick), the seam `IUiInteractionSounds` that the game module implements (`RootLifetimeScope` registers the silent `NullUiInteractionSounds` when the game module registers none), `UiInteractionRelay` on a canvas root plus `builder.RegisterUiInteractionSounds(relay)` in the screen's scope (a per-scope presenter forwards each interaction; slider ticks are throttled to one per 75 ms), and `Shared.UI.UiInteractionEmitter` (hover on pointer enter when interactable, click through `Button.onClick`, so Submit sounds too). `Button.prefab`, `Selector.prefab` (Previous/Next click as Step) and `Slider.prefab` (hover only) carry emitters; `SliderView` emits Tick on user changes, never on `SetValue`; the Settings scene has a relay and its Back button plays Back.

### Changed

- `GameInput` `UI/Submit` also binds `<Keyboard>/space` (next to `*/{Submit}`), so Space triggers `IUIActions.OnSubmit`. uGUI's EventSystem uses its own actions, so Space does not click selected buttons.
- Package EditMode tests pruned: tests without a meaningful assertion, tautologies, duplicates and redundant parameter rows removed (Core.Tests, Bootstrap.Tests, Loading.Tests, Shared.UI.Tests); no production code changed. The boot cover layout check compares offsets with a tolerance.

### Fixed

- Leaving Play mode or quitting while a domain ran logged `MissingReferenceException`s: Unity destroyed the domain's views before the scope's `OnDestroy` disposed its container, so `Dispose` code and the callbacks it triggered (an input lock released into its subscribers, an observable reset) touched destroyed objects, and the first exception aborted the rest of the disposal, leaving tickables running. `DomainLifetimeScope` now disposes its container in `OnApplicationQuit`, while every scene object is still alive, and `DomainRunner`'s teardown skips the scene-set unload of a scope disposed that way.

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
