---
name: launching-domains
description: How domains are composed and launched in this Unity project - the root composition (RootLifetimeScope, the GameModule installer asset and the IMainFlow game flow in Bootstrap), GameFlow and boot modes, calling RunAsync with Transition.Loading or Transition.None, handling the returned OneOf result with Match/Switch, launching leaf and sub-domains from a presenter (SubscribeAwait with AwaitOperation.Drop), parallel long-lived domains, cancellation boundaries, the loading screen handoff, and play-from-any-scene debug runs. Use when changing the game's order of screens/modes, wiring which domain opens which, starting a domain alongside others, or debugging why a domain does not start, end or tear down.
---

# Launching domains and the game flow

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/` (Core, Shared, Bootstrap, Loading, Settings) or the game's `src/Assets/_Project/` (game module, game domains). Rules: `docs/Rules.md` (Boundaries, DI and lifecycle, Async and errors). Runner internals: `Core/Code/Domains/DomainRunner.cs`.

## Composition at the root

- `Bootstrap/Code/RootLifetimeScope.cs` (on `Bootstrap/Prefabs/RootLifetimeScope.prefab`) installs Core, the infrastructure domains it knows, and the game: the `GameModule` asset in its `_gameModule` field (`Bootstrap/Code/GameModule.cs`, a ScriptableObject with `Install(IContainerBuilder)`), or `Bootstrap/Code/IdleMainFlow.cs` when none is assigned. The base prefab keeps `_gameModule` empty; the game owns a prefab variant that sets it and its own `VContainerSettings` asset pointing at the variant, registered as the preloaded VContainerSettings in Player Settings (`Bootstrap/Settings/VContainerSettings.asset` is the idle default; `Bootstrap/Code/Editor/GameModules/RootScopeAssets.cs` switches between them).
- A game module registers the game's main domains (`builder.RegisterDomain<XDomain>(_xDescriptor)`) and one `IMainFlow` (`Bootstrap/Code/IMainFlow.cs`). It lives in its own assembly that references `Bootstrap` and the domains (the shipped sample, if present, is `Sample/Code/SampleGameModule.cs`). Its `IsUiNavigationEnabled` field (off by default) turns on keyboard/gamepad UI navigation on the root EventSystem (skill `ui-views`).
- No game module yet (sample removed): run menu `Tools/Foundation/Create Game Module` (`Bootstrap/Code/Editor/GameModules/GameModuleScaffold.cs`). It creates `Assets/_Project/<Game>/` with `Code/<Game>.asmdef` (+ `csc.rsp`; add the main domains to its references), `<Game>GameModule` and an internal `<Game>MainFlow` stub, then the module asset, the root prefab variant and `<Game>VContainerSettings.asset`, and makes those settings the preloaded ones. Add serialized descriptor fields to the module and assign them on its asset. The play-from-any-scene hook reads the descriptors serialized on the module of the preloaded settings.
- `Loading` (loading screen) and `Settings` (Core settings overlay) are registered directly by `RootLifetimeScope` and are not part of the removable sample.
- `Bootstrap/Code/GameFlow.cs` (normal boot) starts the loading domain, shows the screen, awaits `CoreStartup.RunAsync`, awaits `IMainFlow.RunAsync`, then quits. `Bootstrap/Code/BootMode.cs` picks `GameFlow`, `DebugDomainBoot` (Editor, Play pressed in a scope scene) or nothing (PlayMode test).

## Launching

```csharp
var result = await _gameplay.RunAsync(new GameplayArgs(LevelIndex: 0), Transition.Loading, ct);
result.Switch(
    won => ...,
    lost => ...,
    quitToMenu => ...);
```

- Inject the entry class (`<Name>Domain`) into the launcher; the launcher's scope must register it (`RegisterDomain` / `RegisterSubDomain`), so it gets that scope's `ScopeRef` as parent.
- `Transition.Loading` only for the main flow's domain switches: the screen covers the teardown of the previous domain and the load of the next, and stays up between two `Loading` runs. A launcher that returns from a `Loading` run and then does something else must hide the screen itself (`ILoadingScreen.HideAsync`).
- `Transition.None` for overlays (leaf/sub-domains) and parallel domains.
- Handle results with `Match` (value) or `Switch` (effects); adding a result case breaks every launcher on purpose.
- Linear flows read like scripts: a `while (true)` loop that awaits domains and branches on results (sample: `Sample/Code/SampleMainFlow.cs`, if present).

## From a presenter (overlays)

```csharp
_view.SettingsClicked
    .SubscribeAwait((_, ct) => OpenSettingsAsync(ct).AsValueTask(), AwaitOperation.Drop)
    .AddTo(ref _subscriptions);
```

`OpenSettingsAsync` disables the view, awaits `_settings.RunAsync(new SettingsArgs(), Transition.None, ct)` and re-enables the view in `finally`. A depth-2 domain (sub or leaf) cannot launch anything: it returns a case (`OpenSettings`) and its launcher loops.

## Parallel and long-lived domains

- Start without awaiting (`var run = RunXAsync(ct);`) and observe it at once: a wrapper method that catches `Exception` except `OperationCanceledException` and logs it with `Log.Exception` (only allowed `catch (Exception)` outside adapters; see `RunLoadingAsync` in `Bootstrap/Code/GameFlow.cs`).
- End early through a linked `CancellationTokenSource`; the launcher catches `OperationCanceledException` only around that call and only `when (!ct.IsCancellationRequested)`.
- One running instance per domain type; a second concurrent run throws.

## Failure and teardown

- An entry point that throws fails its domain: `RunAsync` throws to the launcher after teardown. Cancellation propagates as `OperationCanceledException`.
- Teardown always runs: scope disposed, content scenes then scope scene unloaded.

## Debug runs

Press Play with a scope scene open: `Bootstrap/Code/Editor/PlayFromAnySceneHook.cs` boots through the first enabled Build Settings scene (`Bootstrap/Scenes/Bootstrap.unity`), and `Bootstrap/Code/DebugDomainBoot.cs` runs `RunDebugAsync` (args from `<Name>Args.CreateDebug()`), logs the result and exits Play mode. Works only for domains registered at the root.

## Verify

Refresh, recompile, empty console; run the PlayMode smoke test `Bootstrap/Tests/DebugRunnableDomainTests.cs`; press Play in `Bootstrap.unity` and read the console (`docs/UnityCli.md`).
