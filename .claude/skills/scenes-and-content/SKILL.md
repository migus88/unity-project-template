---
name: scenes-and-content
description: How scenes and loadable content work in this Unity project (Unity 6 Content Directories, no Addressables, no Resources) - the one scope scene per domain versus content-only scenes, loading and unloading content scenes through DomainSceneSet, finding views in a loaded content scene, LoadableSceneId and Loadable<T> fields on the domain's DomainContent asset, loading heavy assets through IContentLoader with reference counting, what may and may not live in player-build assets, Build Settings, and building content directories (menu Build/Content Directories) before a player build. Use when adding a level, environment, room or any additional scene, loading assets on demand, or preparing a player build.
---

# Scenes and content

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/` (Core, Shared, Bootstrap, Loading, Settings, Consent) or the game's `src/Assets/_Project/` (game module, game domains). Rules: `docs/Rules.md` (Async and errors, Data). Code: `Core/Code/Content/`, `Core/Code/Domains/DomainSceneSet.cs`.

## Scenes

- Each domain has exactly one **scope scene** (`Domains/<Name>/Scenes/<Name>.unity`, holds the `LifetimeScope` and its views) and any number of **content scenes** (`Domains/<Name>/Scenes/<Name>_<Env>.unity`: geometry, lighting, authored objects; no `LifetimeScope`, no logic).
- Only the boot scene is in Build Settings: the game module's empty `<Game>/Scenes/Boot.unity` (created by Tools/Foundation/Create Game Module or Create Boot Scene; `Bootstrap/Code/Editor/GameBootScene.cs`), else the package's `Bootstrap/Scenes/Bootstrap.unity` as the fallback. Keep it empty (`Bootstrap/Tests/EditMode/BootstrapSceneTests.cs` fails otherwise) and never add domain scenes.
- Never call `SceneManager` load/unload in game code.

## Adding a content scene

- [ ] Create the scene through the Editor in `Domains/<Name>/Scenes/`.
- [ ] Give it one root object with a view component the presenter can locate (for example `<Env>View` exposing spawn points and item views as serialized fields).
- [ ] Reference it from `<Name>Content` (`Domains/<Name>/Code/<Name>Content.cs`): `[field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];` and assign it in the content asset.
- [ ] The flow presenter injects `DomainSceneSet` and the content type, calls `await _scenes.LoadAsync(_content.EnvironmentScenes[i], ct)`, handles `NotFound` (a configuration bug: throw), then finds the root view in the returned `Scene` via `scene.GetRootGameObjects()` + `TryGetComponent`. This lookup is the one allowed `Find`-style exception.
- [ ] Unload with `_scenes.UnloadAsync(scene, ct)` if the domain swaps environments; everything still loaded is unloaded by the runner at teardown.
- [ ] `LocalizedLabel`s in content scenes are bound automatically when loaded through `DomainSceneSet`.
- [ ] With `Transition.Loading`, the loading screen waits for content loads the domain starts during startup and for sub-domains it starts during startup (their scope scene, content loads and first `Start`), so the scene is in before the reveal. A sub-domain launched later (after an `await` in `StartAsync`, on input) is not waited for.

## Loadable assets

- Heavy or optional assets: a `Loadable<T>` field on the content asset (or on a config referenced from it). Load with `IContentLoader.LoadAsync(loadable, ct)` (returns `OneOf<T, NotFound>`), release with `Release(loadable)` in the owner's `Dispose`. Loads are reference-counted per `Loadable` instance. Test reference: `Core/Tests/Content/ContentLoaderTests.cs`.
- Loaded assets are used by authored objects (clips, textures, configs); they are never instantiated.
- Forbidden: `Loadable<T>.Load()` (sync), `Resources.Load`, Addressables.
- Player-build assets (root prefab, `CoreConfig`, domain descriptors, the boot scene) MUST NOT hold `Loadable<T>`/`LoadableSceneId`: they fail or log errors in players. Only the content asset `<Name>Content.asset` (the content directory root) may.

## Content directories and builds

- All descriptors' content is built into one shared directory, `Content/Shared`, whose only root is a generated `ContentCatalog` mapping each `ContentDirectoryName` (the content name, unique) to the descriptor's `EditorContent`; assets used by several domains are stored and loaded once. `HasOwnContentDirectory` on a descriptor builds it into its own directory named `ContentDirectoryName` instead (separately shipped content; shared assets then exist twice). `ContentDirectoryRegistry` merges the catalogs of all directories. The Editor loads everything from the AssetDatabase: no build needed for Play mode.
- Content assets load at boot with the catalog: keep them to `LoadableSceneId`/`Loadable<T>` fields, never direct references to heavy assets.
- Each directory is built as one LZ4HC-compressed archive (`BuildCompression.LZ4`, chunk-based) without type trees and stamped with the Unity version (`ContentDirectoryBuilder.ContentOptions`/`Compression`): a player reads only content built in the same build as itself, so never ship or patch content from another build. The builder logs each directory's size.
- Before a player build run menu `Build/Content Directories` (`Core/Code/Editor/ContentDirectoryBuilder.cs`), e.g. `unity command menu --no-banner --detach --path "Build/Content Directories"`. Its output (StreamingAssets/Content under `src/Assets`) and the temporary `Assets/ContentCatalogBuild` folder are build output and gitignored. The build may re-save URP assets: revert unintended changes.
- A descriptor with `IsDevelopmentOnly` ticked (debug tools, replay viewers) is in the shared directory only in development builds: the menu follows the active Development Build setting, a player build follows `BuildOptions.Development`, `ContentDirectoryBuilder.BuildAll(bool isDevelopmentBuild)` takes it explicitly (custom build scripts call it themselves). Name clashes are still checked in release builds.
- Build environments (package README, "Build environments"): per-environment values live in `BuildEnvironment` assets (`Core/Code/Environment/`); the root prefab references only the `ActiveBuildEnvironment` slot (a direct reference, allowed in player-build assets). The editor-only `BuildEnvironmentMap` maps build profiles to environments; `Core/Code/Editor/Environment/BuildEnvironmentBuildHook.cs` fills the slot for the active profile (an unmapped profile fails the build) and `BuildEnvironmentRestorer.cs` puts the Editor environment back after the build, on load and before Play. Never commit a slot holding a non-Editor environment.
- Details and headless builds: `docs/UnityCli.md`.

## Verify

Press Play in the scope scene and check the console for `NotFound` or scene errors; the PlayMode smoke test checks every content scene unloads at teardown.
