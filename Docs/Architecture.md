# Architecture Specification

> **Audience:** AI agents (an orchestrator and the implementation agents it spawns). This is not a human tutorial.
> **Status:** Approved design, not yet implemented. Everything under `Assets/_Project/` described here does not exist yet.
> **Normative language:** **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, **MAY** follow RFC 2119. A MUST rule may only be broken by a human decision recorded in §17 (Decision Log).
> **Companion document:** `Docs/Coding Conventions.md` (naming, formatting, member ordering, serialization). It is binding. Where this document and the conventions overlap, both apply; where they conflict, this document wins and the conflict is listed in §16.

---

## 0. How to use this document

1. Read the whole file before planning. Sections refer to each other.
2. §1–§3 give the tooling and the layout of the project. §4–§6 give the runtime model: domains, dependency injection, presentation. §7 covers errors. §8–§12 cover the Core services. §13 is the sample game. §14 covers testing. §15 lists unverified assumptions, and those MUST be spiked first. §16 lists rules this document derived that the owner never discussed. §17 is the decision log.
3. Code blocks are **reference skeletons**. They fix names, shapes, and responsibilities. Bodies are indicative. Agents MUST keep the public shapes unless §15 verification forces a change. If it does, update this document in the same change.
4. Split work by section. Suggested implementation order is in §18.

---

## 1. Principles (the owner's world view — use as tie-breakers)

| # | Principle | Consequence |
|---|---|---|
| P1 | Code must be easy to read and modify. | Prefer explicit, linear code: `await` a domain and `Match` its result instead of event chains. Avoid clever abstractions. |
| P2 | Deleting a domain must not break the game (to an extent). | Domains never reference other main domains. A deletion breaks only the few lines that launch the domain, and the compiler finds them. |
| P3 | Domains must not be too granular. | A domain is created only when a feature has **its own lifetime** (§4.1). Small infrastructure goes into **Core**. |
| P4 | Plain C# classes over MonoBehaviours. | Logic lives in plain classes registered in VContainer, including entry points. MonoBehaviours are views only. |
| P5 | Inject MonoBehaviours, never into them. | MonoBehaviours have no `[Inject]`, never call `Resolve`, and know nothing about presenters or services. |
| P6 | Separate presentation from logic. | MVP (§6). Presenters and services manipulate passive views. |
| P7 | Reactive extensions (R3) are glue, not the backbone. | R3 is used for view↔presenter wiring and for observable state. Flow control is `async`/`await`. Operator chains stay short (§6.5). |
| P8 | No exceptions for expected failures. | Discriminated unions (OneOf) for expected failures. Exceptions only for bugs and cancellation (§7). |
| P9 | Async everything, always cancellable. | UniTask throughout. Every async method takes a `CancellationToken`. Assets load asynchronously. |

---

## 2. Tooling

### 2.1 Engine

- Unity **6000.6.3f1** (Unity 6.6). URP 17.6. 3D.
- Target platforms: **Windows, macOS, Linux** (standalone). Mobile and WebGL are out of scope. Do not add workarounds for them.
- Scripting backend: whatever is set in the project (unchanged). Nothing in this design may rely on Reflection.Emit at runtime in player builds. NSubstitute is Editor-only, see §14.

### 2.2 Packages (installed in commit "Add architecture dependencies and MLock submodule")

| Library | Version | Source | Assembly / namespace to reference |
|---|---|---|---|
| VContainer | 1.19.0 | OpenUPM `jp.hadashikick.vcontainer` | `VContainer`, `VContainer.Unity` |
| UniTask | 2.5.11 | OpenUPM `com.cysharp.unitask` | `UniTask`, `UniTask.Linq` (not used), ns `Cysharp.Threading.Tasks` |
| R3 (core) | 1.3.1 | NuGet via NuGetForUnity | ns `R3` |
| R3.Unity | 1.3.1 | git `com.cysharp.r3` | `R3.Unity`, ns `R3` (e.g. `OnClickAsObservable`) |
| OneOf | 3.0.271 | NuGet | ns `OneOf`, `OneOf.Types` |
| OneOf.SourceGenerator | 3.0.271 | NuGet (Roslyn analyzer) | `[GenerateOneOf]` |
| Newtonsoft.Json | 3.2.2 | `com.unity.nuget.newtonsoft-json` | `Newtonsoft.Json` |
| Cinemachine | 3.1.7 | `com.unity.cinemachine` | `Unity.Cinemachine` |
| Input System | 1.20.0 | Unity registry (pre-existing) | `Unity.InputSystem` |
| MLock | 2.1.0 | git submodule `Submodules/MLock`, referenced via `file:` in `Packages/manifest.json` | ns `Migs.MLock`, `Migs.MLock.Interfaces` |
| Odin Inspector & Serializer | 4.x | committed in `Assets/Plugins/Sirenix` (the repo is private) | `Sirenix.OdinInspector`, `Sirenix.Serialization` |
| TextMeshPro / uGUI | ugui 2.6.0 | Unity registry | `Unity.TextMeshPro`, `UnityEngine.UI` |
| Test Framework | 1.8.0 | Unity registry | NUnit |
| NSubstitute | 6.2.0 | NuGet, `autoReferenced=false` | tests only |
| AwesomeAssertions | 9.6.0 | NuGet, `autoReferenced=false` | tests only (Apache-2.0 fork of FluentAssertions; FA 8+ is commercially licensed and **MUST NOT** be used) |
| NuGetForUnity | 4.5.0 | OpenUPM | editor tool |

Package rules:

- NuGet packages live in `Packages/nuget-packages/`. `packages.config` is committed. `InstalledPackages/` is **gitignored** and restored when Unity opens.
- **NuGetForUnity restore installs only what `packages.config` lists — transitive dependencies MUST be listed explicitly** (without `manuallyInstalled`). Test-only packages and their dependencies (NSubstitute, Castle.Core, System.Diagnostics.EventLog, System.Security.Principal.Windows, AwesomeAssertions) are `autoReferenced="false"`. When adding a NuGet package, resolve its dependency closure for .NET Standard 2.1 (fall back to 2.0) and list it all.
- MLock is a submodule so the owner can edit it in place and upstream the changes with a PR. Agents MAY change MLock when needed. Such changes MUST be committed inside the submodule and flagged to the owner. They MUST NOT be copied into `Assets/`.
- Packages that must not be touched in this phase: `com.unity.pipeline`, `com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.collab-proxy`. **Do not remove or reconfigure them.** They belong to a future AI-tooling session.
- Do not add packages that are not listed here without owner approval. Explicitly **not** used: Addressables, Unity Localization (it depends on Addressables), MessagePipe, DOTween, Zenject/Extenject, UniRx, FluentAssertions ≥ 8.

### 2.3 C# language

- Every first-party asmdef (everything under `Assets/_Project/`) has a sibling `csc.rsp`:
  ```
  -langversion:12
  -nullable:enable
  ```
  Do **not** place a root `Assets/csc.rsp`. That would apply to third-party code.
- **No compiler patching** (unity-csharp-patch was evaluated and rejected). C# 13/14 features are unavailable: no `field` keyword, no extension members, no `params` collections.
- **Exactly one polyfill is allowed:** `IsExternalInit`, so that `record class` and `init` work. It lives in Core:
  ```csharp
  // Assets/_Project/Core/Polyfills/IsExternalInit.cs
  namespace System.Runtime.CompilerServices
  {
      public static class IsExternalInit
      {
      }
  }
  ```
  `required` members, `CallerArgumentExpression`, interpolated string handlers, and generic attributes are **forbidden** (they need extra polyfills or crash on Mono). Ref fields, static abstract interface members, and inline arrays are unsupported on this runtime.
- Allowed and encouraged: file-scoped namespaces, `record` / `readonly record struct`, `init`, switch expressions, pattern and list patterns, collection expressions, raw string literals, `global using` (only inside an assembly's own `GlobalUsings.cs`, and sparingly).
- **Nullable reference types are enabled.**
  - Injected constructor parameters are non-nullable. Constructors do not null-check them. VContainer guarantees resolution or throws at build time.
  - Unity-serialized references on MonoBehaviours and ScriptableObjects: `[SerializeField, Required] private Button _playButton = null!;`. The `null!` means "Unity assigns this". `[Required]` is Odin's attribute, so a missing reference shows up in the inspector and validator.
  - "Might be missing" in an API is expressed as a union (`OneOf<T, NotFound>`), **not** as `T?` returned from public service methods. `T?` is fine for private state and local variables.

### 2.4 Odin vs Newtonsoft vs Unity serialization

| Concern | Tool |
|---|---|
| Inspector UX (validation, grouping, buttons, dictionaries in the inspector) | Odin Inspector. Use its attributes freely in runtime code, since Odin is committed. |
| Config ScriptableObjects that need interfaces, dictionaries, or polymorphism | `SerializedScriptableObject` (Odin Serializer). Use it sparingly. Plain `ScriptableObject` + `[SerializeReference]` is preferred when it suffices. |
| Runtime persistence (saves, settings) and JSON data files | Newtonsoft.Json, only through Core's `IJsonSerializer` adapter (§7.6, §10.3). |
| Everything else | Unity serialization. |

Never use Odin Serializer or `JsonUtility` for save data. Never serialize live runtime objects. Serialize DTOs only.

---

## 3. Project structure

### 3.1 Folder layout

```
Assets/
  _Project/                              ← all first-party content (underscore sorts it first)
    Core/
      Core.asmdef                        rootNamespace: Core
      csc.rsp
      Polyfills/IsExternalInit.cs
      Results/                           shared error/result types (§7.3)
      Logging/                           Log, LogTag (§7.7)
      Domains/                           DomainRunner, DomainDescriptor, DomainLifetimeScope, ScopeRef, DomainCompletion, Transition (§4)
      Content/                           ContentLoader, SceneLoader, ContentDirectoryRegistry (§10.1)
      Storage/                           FileStorage, JsonSerializer adapters (§7.6)
      Save/                              SaveStore (§10.3)
      Settings/                          SettingsService (§10.4)
      Input/                             GameInput.inputactions (+ generated GameInput.cs), InputService, InputLockTag (§9.1, §9.2)
      Time/                              TimeService, clocks, TimerService (§9.3)
      Audio/                             AudioService, AudioCue (§9.4)
      Localization/                      LocalizationService, LocalizationTable, TextKey, LocalizedLabel, LocalizedLabelBinder (§10.5)
      Transitions/                       SceneTransitionService, TransitionOverlayView (§9.5)
      Views/                             ViewFactory, ViewPool (§6.6)
      CoreInstaller.cs                   registers every Core service into the root scope
      Editor/
        Core.Editor.asmdef               Editor-only; localization key generator, validators
        csc.rsp
      Tests/
        Core.Tests.asmdef                EditMode
        csc.rsp
    Bootstrap/
      Bootstrap.asmdef                   rootNamespace: Bootstrap
      csc.rsp
      RootLifetimeScope.cs
      GameFlow.cs
      BootMode.cs
      Prefabs/RootLifetimeScope.prefab   camera + CinemachineBrain, EventSystem, transition overlay, audio sources
      Settings/VContainerSettings.asset  RootLifetimeScope = the prefab above
      Scenes/Bootstrap.unity             the ONLY scene in Build Settings
      Editor/
        Bootstrap.Editor.asmdef          play-from-any-scene hook (§4.8)
        csc.rsp
      Tests/
        Bootstrap.PlayModeTests.asmdef   smoke test (§14.4)
        csc.rsp
    Shared/
      UI/
        Shared.UI.asmdef                 reusable view widgets (no scope, no presenters of its own)
        csc.rsp
      TestUtils/
        TestUtils.asmdef                 test-only helpers (union assertions, fakes)
        csc.rsp
    Domains/
      MainMenu/                          main domain
      Gameplay/                          main domain (contains the Pause sub-domain)
      Settings/                          leaf domain (settings overlay reused by MainMenu and Pause)
  Plugins/Sirenix/                       Odin (third-party, untouched)
Submodules/MLock/                        git submodule
Packages/nuget-packages/                 NuGetForUnity
Docs/
```

### 3.2 Folder layout of a domain

```
Domains/<Name>/
  <Name>.asmdef                          rootNamespace: <Name>
  csc.rsp
  <Name>Domain.cs                        public entry class (§4.3)
  <Name>Args.cs                          public args record
  <Name>Result.cs                        public named union + its case types
  <Name>DomainDescriptor.cs              ScriptableObject type (§4.4)
  <Name>LifetimeScope.cs                 DomainLifetimeScope subclass (§5.3)
  <Name>DomainDescriptor.asset
  <Name>Content.asset                    content-directory root asset (§10.1)
  <Name>Text.asset                       localization table (§10.5)
  <Name>Text.g.cs                        generated TextKey constants (§10.5)
  Scripts/                               presenters, services, models, views — subfolders by feature, not by kind
  Scenes/<Name>.unity                    THE scope scene (exactly one)
  Scenes/<Name>_<Env>.unity              optional content-only scenes (no LifetimeScope)
  Configs/                               ScriptableObject configs + their assets
  Prefabs/
  Art/                                   meshes, textures, materials, animations, audio used only by this domain
  <SubDomain>/                           sub-domain folder, same shape minus the asmdef (§4.2)
  Tests/
    <Name>.Tests.asmdef
    csc.rsp
```

- Assets used by exactly one domain MUST live inside that domain's folder. Deleting the folder removes everything.
- Assets used by more than one domain live in `Shared/` (for example `Shared/Art/`, `Shared/UI/`).
- Inside `Scripts/`, group by **feature** (`Scripts/Player/`, `Scripts/Hud/`), not by kind (`Presenters/`, `Views/`).

### 3.3 Assemblies and reference rules

| Assembly | MAY reference (first-party) | MUST NOT reference |
|---|---|---|
| `Core` | nothing first-party | everything else first-party |
| `Core.Editor` | `Core` | domains, Bootstrap |
| `Shared.UI` | `Core` | domains, Bootstrap |
| Leaf domain (e.g. `Settings`) | `Core`, `Shared.*` | any other domain, Bootstrap |
| Main domain (e.g. `Gameplay`) | `Core`, `Shared.*`, leaf domains | other main domains, Bootstrap |
| `Bootstrap` | everything | — |
| `Bootstrap.Editor` | `Bootstrap`, `Core` | — |
| `<X>.Tests` | `<X>`, its allowed references, `TestUtils` | — |
| `TestUtils` | `Core` | domains |

asmdef settings:

- Every first-party runtime asmdef: `"autoReferenced": false`, `rootNamespace` set, no `allowUnsafeCode`.
- Third-party references are added per assembly only when that assembly uses them. For example, only assemblies with views reference `Unity.TextMeshPro`.
- Test asmdefs:
  - `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"overrideReferences": true`.
  - `precompiledReferences`: `nunit.framework.dll`, `NSubstitute.dll`, `Castle.Core.dll`, `AwesomeAssertions.dll` (plus their dependency DLLs as restored), and whichever of `R3.dll` / `OneOf.dll` the tests use.
  - EditMode tests: `"includePlatforms": ["Editor"]`.
- Namespaces: bare, equal to the assembly name plus the folder path under `Scripts/`. For example `Gameplay`, `Gameplay.Player`, `Gameplay.Pause`, `Core.Save`. **No company or game prefix.**

---

## 4. Domains

### 4.1 Definition

A **domain** is a feature with **its own lifetime**: it is created, lives for a while, and is torn down, usually together with scenes. Test for a new domain: *"Is there a moment this thing starts and a moment it ends, and does it own UI, scenes, or state for that span?"* If not, it belongs in Core (app-lifetime infrastructure) or inside an existing domain.

Each domain has:

- exactly one asmdef (sub-domains share their parent's; see §4.2),
- exactly one **scope scene**, which holds its `LifetimeScope` and the views for its presenters,
- zero or more **content scenes** (environment, lighting, geometry), which have no `LifetimeScope`,
- one **content directory** (§10.1),
- one public **entry class** plus public `Args` and `Result` types. **Everything else in the assembly is `internal`.**

### 4.2 Kinds and nesting

| Kind | Lives in | Launched by | Scope parent | May launch |
|---|---|---|---|---|
| **Main** | own asmdef | Bootstrap (`GameFlow`) | root | its sub-domains, leaf domains |
| **Sub** | subfolder of its main domain, **same asmdef**, namespace `<Main>.<Sub>` | only its main domain | the main domain's scope | nothing |
| **Leaf** | own asmdef, references only Core and Shared | Bootstrap or any main domain | the launcher's scope | nothing |

- Maximum depth: **root (0) → main (1) → sub or leaf (2)**. `DomainRunner` enforces it. A launch whose parent depth is ≥ 2 is a bug and **throws** `InvalidOperationException`.
- A sub-domain MAY resolve its parent's services through DI. That is the reason it shares the assembly.
- A leaf domain MUST only depend on Core and Shared, because it may be parented to different domains.
- Deleting a sub-domain means deleting its folder and its call site. Deleting a leaf or main domain means deleting its folder and fixing the compile errors in the launchers (Bootstrap or main domains).

### 4.3 Entry contract

Each domain exposes exactly one public entry class:

```csharp
namespace Gameplay
{
    public sealed class GameplayDomain : IDebugRunnableDomain
    {
        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly GameplayDomainDescriptor _descriptor;

        public GameplayDomain(DomainRunner runner, ScopeRef launcherScope, GameplayDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

        public DomainDescriptor Descriptor => _descriptor;

        public UniTask<GameplayResult> RunAsync(GameplayArgs args, Transition transition, CancellationToken ct)
        {
            return _runner.RunAsync<GameplayArgs, GameplayResult>(_descriptor, _launcherScope, args, transition, ct);
        }

#if UNITY_EDITOR
        public async UniTask<object> RunDebugAsync(CancellationToken ct)
        {
            return await RunAsync(GameplayArgs.CreateDebug(), Transition.None, ct);
        }
#endif
    }

    public sealed record GameplayArgs(int LevelIndex)
    {
#if UNITY_EDITOR
        public static GameplayArgs CreateDebug() => new(LevelIndex: 0);
#endif
    }

    [GenerateOneOf]
    public partial class GameplayResult : OneOfBase<GameplayResult.Won, GameplayResult.Lost, GameplayResult.QuitToMenu>
    {
        public readonly record struct Won(int Score, TimeSpan Time);
        public readonly record struct Lost(int Score);
        public readonly record struct QuitToMenu;
    }
}
```

Rules:

- The signature is always `UniTask<TResult> RunAsync(TArgs args, Transition transition, CancellationToken ct)`. `TArgs` is a `sealed record` (use an empty record if there are no args). `TResult` is a **named union** (§7.2). A domain that never ends by itself (long-lived, for example a HUD running alongside Gameplay) still declares a result type. It ends only through cancellation, and its union typically has a single case.
- `CreateDebug()` MUST exist on every main and leaf domain's args, under `#if UNITY_EDITOR`.
- The entry class is registered in the **launcher's** scope (§5.2) and injects the launcher's `ScopeRef`. That is how the runner knows the parent.

### 4.4 Domain descriptor

```csharp
namespace Core.Domains
{
    public abstract class DomainDescriptor : ScriptableObject
    {
        [field: SerializeField, Required] public string ContentDirectoryName { get; private set; } = null!;
        [field: SerializeField] public LoadableSceneId ScopeScene { get; private set; }
        [field: SerializeField] public LogTag LogTag { get; private set; }

#if UNITY_EDITOR
        [field: SerializeField, Required] public UnityEditor.SceneAsset EditorScopeScene { get; private set; } = null!;
#endif
    }
}

namespace Gameplay
{
    [CreateAssetMenu(menuName = "Domains/Gameplay Descriptor")]
    public sealed class GameplayDomainDescriptor : DomainDescriptor
    {
        [field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];
    }
}
```

Content scenes and domain configs MAY be referenced from the concrete descriptor. Configs registered in the domain scope usually live on the `<Name>LifetimeScope` component instead (§5.3). `EditorScopeScene` is used only by the play-from-any-scene hook (§4.8). Whether `LoadableSceneId` can be derived from a `SceneAsset` automatically is part of §15.1.

### 4.5 `DomainRunner` (Core)

Responsibilities, in order:

1. **Guards.** One running instance per descriptor type (a second run is a bug, so it throws). Parent depth < 2 (throws).
2. `await transitions.ShowAsync(transition, ct)`.
3. Take the **load gate** (a `SemaphoreSlim(1)`). `LifetimeScope.EnqueueParent` / `Enqueue` are static and process-wide, so two domains loading concurrently would race. The gate serializes *only the scene-load + scope-build window*. Domains still *run* in parallel.
4. Inside `using (LifetimeScope.EnqueueParent(parent.Scope))` and `using (LifetimeScope.Enqueue(builder => ...))`:
   - load the scope scene additively (`ISceneLoader`, §10.1),
   - the extra installer registers: `args` (as `TArgs`), a new `DomainCompletion<TResult>`, and the per-domain infrastructure entry points (`LocalizedLabelBinder`, §10.5).
5. Release the gate. Find the built scope: `LifetimeScope.Find<DomainLifetimeScope>(scene)`, or the scope's own registration callback.
6. `await transitions.HideAsync(transition, ct)`.
7. `return await completion.Task` (with `ct` attached).
8. `finally` (always, also on cancellation):
   - dispose the scope (this disposes every `IDisposable` registered in it and cancels `IAsyncStartable` tokens),
   - unload the content scenes the domain loaded through its `DomainSceneSet` (§4.6), then the scope scene,
   - release the "running" guard.

   Teardown uses `CancellationToken.None`. Teardown MUST complete.

```csharp
namespace Core.Domains
{
    public sealed class DomainRunner
    {
        public UniTask<TResult> RunAsync<TArgs, TResult>(DomainDescriptor descriptor, ScopeRef parent, TArgs args, Transition transition, CancellationToken ct)
            where TArgs : class
            where TResult : class;
    }

    public sealed class DomainCompletion<TResult> where TResult : class
    {
        public UniTask<TResult> Task { get; }
        public bool IsCompleted { get; }
        public void Complete(TResult result);   // second call is a bug → throws
    }

    public sealed record ScopeRef(LifetimeScope Scope, int Depth);

    public enum Transition
    {
        None = 0,
        Fade = 1,
    }

    public interface IDebugRunnableDomain
    {
        DomainDescriptor Descriptor { get; }
#if UNITY_EDITOR
        UniTask<object> RunDebugAsync(CancellationToken ct);
#endif
    }
}
```

`ScopeRef` registration: `RootLifetimeScope` registers `new ScopeRef(this, 0)`. `DomainLifetimeScope.Configure` registers `new ScopeRef(this, parentDepth + 1)`, where `parentDepth` is resolved from the parent container. Because child registrations shadow the parent's, anything resolved inside a scope gets **that** scope's `ScopeRef`.

Ending a domain: any presenter or service inside the domain injects `DomainCompletion<TResult>` and calls `Complete(...)`. Usually exactly one "flow" presenter owns the completion. Scattering `Complete` calls is discouraged.

### 4.6 Content scenes inside a domain

Only the domain's own code loads and unloads content scenes, through `DomainSceneSet`. It is a scoped Core service, registered automatically in every domain scope, that loads `LoadableSceneId`s additively and unloads everything it loaded when the scope is disposed. Content scenes contain no `LifetimeScope` and no logic. Their MonoBehaviours are views or plain scene objects. When presenters need views from a content scene, the domain's flow presenter locates them after load, typically by a known root component, and passes them on. Views in content scenes are not registered through the scope's serialized fields.

### 4.7 Parallel domains

- A launcher MAY run several domains concurrently, for example `await UniTask.WhenAny(gameplay.RunAsync(...), hud.RunAsync(...))`.
- Long-lived domains end through cancellation. The launcher creates a linked `CancellationTokenSource`, cancels it, and the runner's `finally` tears the domain down. The resulting `OperationCanceledException` propagates to the launcher, which handles it deliberately (§7.5).
- Only one instance per domain type runs at a time.

### 4.8 Boot, the game flow, and play-from-any-scene

**Root scope.** `RootLifetimeScope` (Bootstrap) is the `VContainerSettings.RootLifetimeScope` prefab. It is instantiated automatically before the first scene, and nothing ever destroys it. It contains the Main `Camera` with `CinemachineBrain`, the `EventSystem` with `InputSystemUIInputModule`, the transition overlay canvas, the audio source pool root, and the `AudioMixer` reference. Its `Configure`:

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.RegisterInstance(new ScopeRef(this, 0));
    CoreInstaller.Install(builder, _coreConfig);           // every Core service (§8)
    builder.RegisterDomain<MainMenuDomain>(_mainMenuDescriptor);
    builder.RegisterDomain<GameplayDomain>(_gameplayDescriptor);
    builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);

    switch (BootMode.Current)
    {
        case BootMode.Normal:
            builder.RegisterEntryPoint<GameFlow>();
            break;
#if UNITY_EDITOR
        case BootMode.DebugDomain:
            builder.RegisterEntryPoint<DebugDomainBoot>();
            break;
#endif
    }
}
```

`RegisterDomain<TDomain>(descriptor)` is a Core extension. It registers the descriptor instance as its concrete type and `TDomain` as a singleton `.AsSelf().As<IDebugRunnableDomain>()`.

**GameFlow** (Bootstrap): an `IAsyncStartable` that reads like a script. It is the only place that knows the order of the main domains.

```csharp
public async UniTask StartAsync(CancellationToken ct)
{
    while (true)
    {
        var menuResult = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Fade, ct);
        var shouldQuit = menuResult.Match(
            play => false,
            quit => true);

        if (shouldQuit)
        {
            _application.Quit();
            return;
        }

        var gameplayResult = await _gameplay.RunAsync(new GameplayArgs(LevelIndex: 0), Transition.Fade, ct);
        gameplayResult.Switch(
            won => Log.Info(LogTags.Flow, $"Won with {won.Score}"),
            lost => Log.Info(LogTags.Flow, $"Lost with {lost.Score}"),
            quitToMenu => { });
    }
}
```

**Build settings.** Only `Bootstrap.unity` is in Build Settings. It is effectively empty; its presence as the first scene means `BootMode.Normal`. Domain scenes are loaded from content directories (§15.1 verifies that this needs no Build Settings entry).

**Play from any scene (Editor only).**

1. `Bootstrap.Editor` has an `[InitializeOnLoad]` hook on `EditorApplication.playModeStateChanged` (`ExitingEditMode`). It records the active scene's path in `SessionState`. If that scene is a registered domain scope scene, the hook sets `EditorSceneManager.playModeStartScene` to `Bootstrap.unity`. Otherwise it clears it.
2. At runtime, `BootMode.Current` is resolved before the root builds: `Normal` in builds; in the Editor, `DebugDomain` if the recorded scene path matches some `IDebugRunnableDomain.Descriptor.EditorScopeScene`.
3. `DebugDomainBoot` (Editor-only entry point) resolves `IReadOnlyList<IDebugRunnableDomain>`, finds the matching domain, awaits `RunDebugAsync(ct)`, logs the result, and then sets `EditorApplication.isPlaying = false`.
4. Sub-domain scenes cannot be debug-run on their own, because they need their parent's services. When one is detected, the hook logs a warning and boots `Normal`.
5. The Editor restores the originally open scenes after Play mode (Unity's default behaviour with `playModeStartScene`).

---

## 5. Dependency injection and lifetime (VContainer)

### 5.1 Scope tree

```
Root (RootLifetimeScope prefab, depth 0) ── Core services, domain entry classes, GameFlow
 ├─ MainMenu scope (depth 1)
 │   └─ Settings scope (leaf, depth 2)
 └─ Gameplay scope (depth 1)
     ├─ Pause scope (sub, depth 2)
     │   (Pause cannot launch Settings: depth limit. Pause asks Gameplay to launch Settings, see §13.)
     └─ Settings scope (leaf, depth 2)
```

### 5.2 Registration rules

- **Constructor injection only**, into plain C# classes. `[Inject]` on fields, properties, or methods is **forbidden** everywhere. MonoBehaviours are **never** injected into.
- MonoBehaviours enter the container as **instances**. Prefer `[SerializeField]` references on the scope component + `builder.RegisterComponent(_view)`. `RegisterComponentInHierarchy<T>()` is allowed for singletons in the scope scene. Explicit serialized references are preferred.
- Presenters and anything that needs a lifecycle: `builder.RegisterEntryPoint<T>()`. Use `.AsSelf()` if something else needs to resolve it.
- Services: `builder.Register<IFoo, Foo>(Lifetime.Singleton)` (a singleton *per scope*). Use an interface when there is a real seam (tests, multiple implementations, or Core contracts implemented by domains). Otherwise register the concrete type. `Lifetime.Transient` needs a reason.
- Configs (ScriptableObjects): `builder.RegisterInstance(_config)` from a serialized field on the scope.
- `IObjectResolver` MAY only be injected by factories (`ViewFactory`, VContainer `RegisterFactory` lambdas) and by `DomainRunner`. Everyone else gets their dependencies explicitly.
- `LifetimeScope.Find`, `FindObjectOfType`, `GameObject.Find`, and singletons (`static Instance`) are **forbidden** in game code. Exceptions: `DomainRunner` finding the freshly built scope, and the flow presenter locating content-scene roots (§4.6).
- Optional cross-domain contracts (escape hatch from §4, use rarely): Core declares `IFoo`. Bootstrap registers the real implementation (from a domain) in the root, **or** registers a Core-provided `NullFoo` when that domain is deleted. Consumers never check for presence.
- High managed-code stripping can break reflection-based injection (VContainer issue #863). Keep Managed Stripping Level at its current setting, or add `link.xml` entries for first-party assemblies. The VContainer source generator MAY be added later. Not now.

### 5.3 `DomainLifetimeScope`

```csharp
namespace Core.Domains
{
    public abstract class DomainLifetimeScope : LifetimeScope
    {
        protected sealed override void Configure(IContainerBuilder builder)
        {
            var parentDepth = Parent.Container.Resolve<ScopeRef>().Depth;
            builder.RegisterInstance(new ScopeRef(this, parentDepth + 1));
            builder.Register<DomainSceneSet>(Lifetime.Singleton);
            ConfigureDomain(builder);
        }

        protected abstract void ConfigureDomain(IContainerBuilder builder);
    }
}

namespace Gameplay
{
    internal sealed class GameplayLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private GameplayConfig _config = null!;
        [SerializeField, Required] private HudView _hudView = null!;
        [SerializeField, Required] private PlayerView _playerView = null!;
        [SerializeField, Required] private PauseDomainDescriptor _pauseDescriptor = null!;
        [SerializeField, Required] private SettingsDomainDescriptor _settingsDescriptor = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.RegisterComponent(_hudView);
            builder.RegisterComponent(_playerView);
            builder.Register<ScoreModel>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameplayFlowPresenter>();
            builder.RegisterEntryPoint<HudPresenter>();
            builder.RegisterEntryPoint<PlayerInputHandler>();
            builder.RegisterEntryPoint<PlayerMovementPresenter>();
            builder.RegisterDomain<PauseDomain>(_pauseDescriptor);
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
        }
    }
}
```

Scope scene `LifetimeScope` inspector settings: `autoRun = true`, parent reference **empty** (the runner supplies it through `EnqueueParent`). A scope scene MUST NOT be opened in Play mode without the runner. The play-from-any-scene hook guarantees this.

### 5.4 Entry points and lifecycle interfaces

| Interface (VContainer) | Use for |
|---|---|
| `IInitializable` | synchronous setup that must happen before `Start` (rare) |
| `IStartable` | subscribe to views, input callbacks, lock service, and state |
| `IAsyncStartable` | async flows. Its `StartAsync(CancellationToken)` token is cancelled when the scope is disposed. **This is the domain's primary lifetime token.** |
| `ITickable` / `IFixedTickable` / `ILateTickable` | **only** to apply continuous values every frame (movement, camera), or to drive `TimerService`. Not for polling input and not for flow. |
| `IDisposable` | release subscriptions, remove input callbacks, unsubscribe from locks, dispose `DisposableBag` |

### 5.5 Async and cancellation rules (UniTask)

- Every async method returns `UniTask` / `UniTask<T>` (never `Task`, except test methods, §14), and takes `CancellationToken ct` as its **last parameter, without a default value**.
- The token comes from the caller. The root of every chain is either `IAsyncStartable.StartAsync`'s token (scope lifetime) or a linked source owned by the code that may cancel early.
- `async void` is **forbidden**. Fire-and-forget: an `async UniTaskVoid` method + `.Forget()`. The method takes a token and handles its own failures. This is allowed only where nothing awaits the outcome (for example a view animation triggered by a click).
- MonoBehaviour async code (views only, for visuals) uses `destroyCancellationToken`, linked with the caller's token when one is given.
- Waiting: `UniTask.Delay(..., cancellationToken: ct)`, `UniTask.Yield(PlayerLoopTiming.Update, ct)`. **Game timers that must stay aligned use `TimerService`** (§9.3), not ad-hoc delays.
- Do not use `.GetAwaiter().GetResult()`, `.Result`, `Task.Run`, or threads for game logic.

---

## 6. Presentation (MVP)

### 6.1 Roles

| Role | Type | Knows | Never |
|---|---|---|---|
| **View** | `MonoBehaviour`, suffix `View` | its serialized children (buttons, labels, transforms, animators) | presenters, services, models, DI, game rules |
| **Presenter** | plain C# class, suffix `Presenter` (`InputHandler` for input translators, §9.1) | its views (concrete types), services, models, `DomainCompletion` | `GetComponent`, `Find`, Unity lifecycle methods |
| **Model / State** | plain C# class, suffix `Model` (mutable state holder) or `State` (immutable snapshot record) | nothing about Unity presentation | views |
| **Service** | plain C# class, suffix `Service` (Core and domain-level) | models, other services, Core adapters | views |

Presenters depend on **concrete view classes** (no `IFooView` interfaces by default). Add an interface only when a presenter is complex enough to deserve unit tests (§14.2).

### 6.2 View rules

- **Outputs** (user intent) are `Observable<T>` properties built from UI components:
  ```csharp
  public Observable<Unit> PlayClicked => _playButton.OnClickAsObservable();
  ```
- **Inputs** are imperative methods starting with a verb: `SetScore(int score)`, `ShowWinPanel()`, `PlayHitAsync(CancellationToken ct)`.
- A view MAY contain **purely visual** code: animations, tweens (Unity Animator or hand-written coroutine-free UniTask loops), particle triggers, layout. It MUST NOT decide game outcomes.
- A view MAY read its own serialized visual config (colors, durations).
- One MonoBehaviour per file. The file name matches the class name.
- Unity messages allowed in views: `Awake` (caching visual components only), `OnValidate`, `OnDrawGizmos`. `Update` / `FixedUpdate` in views are **discouraged**. Movement and continuous logic are driven by presenters through `ITickable` calling view methods.

```csharp
namespace MainMenu
{
    internal sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField, Required] private Button _playButton = null!;
        [SerializeField, Required] private Button _settingsButton = null!;
        [SerializeField, Required] private Button _quitButton = null!;
        [SerializeField, Required] private TMP_Text _versionLabel = null!;

        public Observable<Unit> PlayClicked => _playButton.OnClickAsObservable();
        public Observable<Unit> SettingsClicked => _settingsButton.OnClickAsObservable();
        public Observable<Unit> QuitClicked => _quitButton.OnClickAsObservable();

        public void SetVersion(string version)
        {
            _versionLabel.text = version;
        }

        public void SetInteractable(bool isInteractable)
        {
            _playButton.interactable = isInteractable;
            _settingsButton.interactable = isInteractable;
            _quitButton.interactable = isInteractable;
        }
    }
}
```

### 6.3 Presenter rules

```csharp
namespace MainMenu
{
    internal sealed class MainMenuPresenter : IStartable, IDisposable
    {
        private readonly MainMenuView _view;
        private readonly IApplicationService _application;
        private readonly SettingsDomain _settingsDomain;
        private readonly DomainCompletion<MainMenuResult> _completion;
        private readonly CancellationTokenSource _lifetime = new();

        private DisposableBag _subscriptions;

        public MainMenuPresenter(MainMenuView view, IApplicationService application, SettingsDomain settingsDomain, DomainCompletion<MainMenuResult> completion)
        {
            _view = view;
            _application = application;
            _settingsDomain = settingsDomain;
            _completion = completion;
        }

        public void Start()
        {
            _view.SetVersion(_application.Version);
            _view.PlayClicked.Subscribe(_ => _completion.Complete(new MainMenuResult.Play())).AddTo(ref _subscriptions);
            _view.QuitClicked.Subscribe(_ => _completion.Complete(new MainMenuResult.Quit())).AddTo(ref _subscriptions);
            _view.SettingsClicked
                .SubscribeAwait((_, ct) => OpenSettingsAsync(ct), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private async ValueTask OpenSettingsAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
            await _settingsDomain.RunAsync(new SettingsArgs(), Transition.None, linked.Token);
            _view.SetInteractable(true);
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _subscriptions.Dispose();
        }
    }
}
```

(Implementation note: R3's `SubscribeAwait` uses `ValueTask`. Wrapping a UniTask with `.AsValueTask()` is acceptable. Alternatively, write the handler as `async UniTaskVoid` with a guard flag. Pick one pattern in Core/Shared and use it consistently. See §16.)

- Presenters own a `DisposableBag` (R3 struct). Every subscription is added to it with `.AddTo(ref _subscriptions)`. `Dispose()` disposes it.
- Presenters that start async work own their lifetime through `IAsyncStartable`'s token, or through a private `CancellationTokenSource` cancelled in `Dispose()`.
- One presenter per view by default. One presenter MAY drive several related views. A view MUST NOT be driven by two presenters.

### 6.4 Models and observable state

- State that one presenter reads is a plain field or property.
- State that several parties observe is a `ReactiveProperty<T>`, kept **private**, and exposed as `ReadOnlyReactiveProperty<T>`:
  ```csharp
  internal sealed class ScoreModel : IDisposable
  {
      private readonly ReactiveProperty<int> _score = new(0);

      public ReadOnlyReactiveProperty<int> Score => _score;

      public void Add(int points)
      {
          _score.Value += points;
      }

      public void Dispose()
      {
          _score.Dispose();
      }
  }
  ```
- One-off notifications from services: a private `Subject<T>` exposed as `Observable<T>`. Public `Subject`s are forbidden.
- **Plain C# `event`s are not used** in first-party code. Use R3 for anything subscribable, for one mechanism with uniform disposal. (The conventions document explains events for the cases where they would be used. This architecture chooses not to use them.)

### 6.5 R3 usage limits (P7)

- Allowed in presenters and services: `Subscribe`, `Where`, `Select`, `DistinctUntilChanged`, `ThrottleFirst`, `Debounce`, `CombineLatest` (for binding two or three properties to one view), `SubscribeAwait`, `Skip`, `Take`.
- A single chain SHOULD have at most **three operators** before `Subscribe`.
- **Forbidden:** using observables for control flow between domains or services, `SelectMany` flattening of async flows, multi-hop pipelines that route events through several services, and `Subject`s used as a message bus. Flow is `async`/`await`. Cross-domain communication is `RunAsync` results.
- Frame and time operators use R3's Unity providers. Game-aligned ticking comes from `TimerService`.

### 6.6 Dynamic views and pooling

```csharp
namespace Core.Views
{
    public interface IViewFactory
    {
        UniTask<OneOf<TView, NotFound>> CreateAsync<TView>(Loadable<GameObject> prefab, Transform parent, CancellationToken ct)
            where TView : Component;

        void Destroy<TView>(TView view) where TView : Component;
    }

    public interface IViewPool<TView> : IDisposable where TView : Component
    {
        UniTask<OneOf<TView, NotFound>> RentAsync(Transform parent, CancellationToken ct);
        void Return(TView view);
    }
}
```

- Views are instantiated with `Object.InstantiateAsync` after the prefab loads through `IContentLoader`. Views have no injection, so no `IObjectResolver.Instantiate` is needed.
- The pool interface exists from day one. The first implementation is a simple stack. Pools are created per domain (for example `ViewPoolFactory.Create<SlotView>(prefab)`) and disposed with the scope.
- **Default: one presenter drives a collection of item views** (for example `InventoryPresenter` owns `List<SlotView>`). Create per-item presenters (through a VContainer `Func<SlotView, SlotPresenter>` factory registered with `RegisterFactory`) only when an item has substantial logic of its own.

---

## 7. Errors, results, logging

### 7.1 Policy

| Situation | Mechanism |
|---|---|
| Expected failure (missing file, parse failure, not enough gold, content not found, validation failure) | Return a **union** (OneOf). |
| Bug or broken invariant (null where impossible, double `Complete`, depth > 2, misconfigured descriptor) | **Throw** (`InvalidOperationException`, `ArgumentException`) and fail loudly. Do not catch. |
| Cancellation | UniTask's `OperationCanceledException`, the idiomatic flow. Not converted to unions. |
| Third-party API that throws for expected conditions (File IO, Newtonsoft, platform APIs) | Wrap in an **edge adapter** that catches the *specific* exceptions and returns unions (§7.6). |

`try/catch` appears **only** in edge adapters, in `DomainRunner`/`GameFlow` cancellation boundaries (§7.5), and in `finally` teardown. `catch (Exception)` is forbidden outside edge adapters. Where an adapter uses it, it MUST rethrow `OperationCanceledException`.

### 7.2 Union style

- **Public** API (domain results, service return types): **named unions** via `OneOf.SourceGenerator`:
  ```csharp
  [GenerateOneOf]
  public partial class LoadSaveResult : OneOfBase<SaveData, NotFound, Corrupted>
  {
  }
  ```
  Named unions MUST be declared inside a namespace (the generator emits into the containing namespace). If the generator fails on Unity 6.6 (§15.2), hand-write them: `public sealed class X : OneOfBase<A, B> { private X(OneOf<A, B> v) : base(v) {} public static implicit operator X(A a) => new(a); ... }`.
- **Private and local** plumbing MAY use inline `OneOf<A, B>`.
- Case types are `readonly record struct` (payload-less or small) or `sealed record` (richer payloads). Result-specific cases are **nested** in the union class (`GameplayResult.Won`). Reusable cases live in `Core.Results`.
- Consume with `Match` (returns a value) or `Switch` (side effects), or `TryPickT0(out var value, out var remainder)` for early return. **`AsT0`/`AsT1` without a prior `IsT0` check is forbidden** (it throws).
- Success without a value: `OneOf.Types.Success`.
- Adding a case to a union intentionally breaks every `Match`/`Switch`. That is the point. Fix all of them.

### 7.3 Shared result types (`Core.Results`)

```csharp
namespace Core.Results
{
    public readonly record struct NotFound;
    public readonly record struct Corrupted(string Reason);
    public readonly record struct Error(string Message);   // generic fallback; prefer specific types
}
```

There is **no** global `Error` hierarchy or error-code enum. Each domain defines its own specific error cases. `Error(string)` is only for truly generic adapter failures.

### 7.4 Handling rule

An error is **logged where it is handled, never where it is created**. A method that returns `NotFound` does not log. The caller that decides "fall back to defaults" logs, if logging is warranted.

### 7.5 Cancellation boundaries

- `DomainRunner`: `finally` teardown. It does not catch cancellation, which propagates to the launcher.
- Launchers that cancel a child on purpose (parallel long-lived domains, closing an overlay) catch `OperationCanceledException` **only around that call**, and only when their own token is not cancelled:
  ```csharp
  try
  {
      await _hud.RunAsync(new HudArgs(), Transition.None, hudCts.Token);
  }
  catch (OperationCanceledException) when (!ct.IsCancellationRequested)
  {
  }
  ```
- Alternatively use UniTask's `SuppressCancellationThrow()` at the same boundary. Either is fine; choose the one that reads better.

### 7.6 Edge adapters (Core)

| Adapter | Wraps | Returns |
|---|---|---|
| `IFileStorage` | `System.IO` (read, atomic write via temp file + `File.Replace`/`Move`, delete, exists) | `OneOf<string, NotFound, Error>`, `OneOf<Success, Error>` |
| `IJsonSerializer` | Newtonsoft (`JsonConvert` with shared `JsonSerializerSettings`: camelCase, `TypeNameHandling.None`, ignore nulls, `DateTime` UTC) | `OneOf<T, Corrupted>` / `string` |
| `IContentLoader`, `ISceneLoader` | Content Directories (`Loadable<T>`, `LoadableSceneId`) | `OneOf<T, NotFound>` |

Game code never touches `System.IO`, `JsonConvert`, `Loadable<T>.LoadAsync`, or `SceneManager` directly.

### 7.7 Logging

```csharp
namespace Core.Logging
{
    [Serializable]
    public readonly record struct LogTag(string Name);

    public static class Log
    {
        [Conditional("GAME_LOG_VERBOSE")] public static void Verbose(LogTag tag, string message);
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")] public static void Info(LogTag tag, string message);
        public static void Warn(LogTag tag, string message);
        public static void Error(LogTag tag, string message);
    }
}
```

- `Log` is static on purpose: logging is infrastructure and is not injected.
- Each assembly declares `internal static class LogTags { public static readonly LogTag Gameplay = new("Gameplay"); ... }`.
- Output format: `[Tag] message`. Direct `Debug.Log*` calls are forbidden outside `Log`.
- `LogTag` is `[Serializable]` for use in descriptors. If a `readonly record struct` does not serialize in Unity, make it a plain `[Serializable] struct` with a `[SerializeField] string _name` (§16).

---

## 8. Core service catalogue

Everything below is registered in the **root** scope by `CoreInstaller.Install(builder, coreConfig)` unless marked *per-domain*. `CoreConfig` is a ScriptableObject on the root prefab that holds the mixer, the overlay view, the audio pool size, the supported languages, and similar values.

| Service | Interface / type | Section |
|---|---|---|
| Domain runner | `DomainRunner` | §4.5 |
| Domain scene set | `DomainSceneSet` *(per-domain, auto)* | §4.6 |
| Content loading | `IContentLoader`, `ISceneLoader`, `ContentDirectoryRegistry` | §10.1 |
| Storage adapters | `IFileStorage`, `IJsonSerializer` | §7.6 |
| Save | `ISaveStore` | §10.3 |
| Settings | `ISettingsService` | §10.4 |
| Input | `GameInput` (generated), `IInputService`, `ILockService<InputLockTag>` | §9.1, §9.2 |
| Time | `ITimeService`, `IRealClock`, `IGameClock`, `ITimerService` | §9.3 |
| Audio | `IAudioService` | §9.4 |
| Transitions | `ISceneTransitionService` | §9.5 |
| Localization | `ILocalizationService`; `LocalizedLabelBinder` *(per-domain, auto)* | §10.5 |
| Views | `IViewFactory`, `ViewPoolFactory` | §6.6 |
| Application | `IApplicationService` (Quit, version, platform), a seam over `Application` for testability | — |

Not in Core: localization through Unity's package, networking, analytics, achievements, ads. Add these later as Core services or leaf domains through the escape hatch in §5.2.

---

## 9. Core runtime services

### 9.1 Input

- One asset: `Core/Input/GameInput.inputactions`, with **Generate C# Class** on: class `GameInput`, namespace `Core.Input`. The existing `Assets/URP/InputSystem_Actions.inputactions` is the starting point. Move or rename it, then remove the project-wide actions reference from Input System settings so the actions exist only once (§16).
- Action maps (initial): `Player` (Move, Look, Jump, Interact, Pause), `UI` (Navigate, Submit, Cancel, Point, Click, ScrollWheel), `Camera` if needed.
- `GameInput` is registered as a root singleton (`builder.Register<GameInput>(Lifetime.Singleton)`), so VContainer disposes it.
- **Reading input uses the generated callback interfaces** (`GameInput.IPlayerActions`), **not polling**:
  - One **input handler** per (domain, map), suffix `InputHandler`. It implements the generated interface, calls `AddCallbacks(this)` in `Start` and `RemoveCallbacks(this)` in `Dispose`, and translates raw input into domain intent: it writes to a model (for example `PlayerInputState.Move`) or calls a service method (`_pause.Request()`).
  - Discrete actions act directly in the callback (on `context.performed`).
  - Continuous actions (Move, Look) cache the value in the callback (`performed` → value, `canceled` → zero). A separate `ITickable`/`IFixedTickable` presenter **applies** it each frame.
  - Generated interfaces require every action of the map to be implemented. Unused actions get an empty body with a comment. This is the reason for keeping handlers per map.
- `IInputService` owns **map activation** as a stack:
  ```csharp
  public interface IInputService
  {
      GameInput Actions { get; }
      IDisposable Push(InputMaps maps);    // enables exactly `maps`; disposing restores the previous set
  }

  [Flags]
  public enum InputMaps
  {
      None = 0,
      Player = 1 << 0,
      Ui = 1 << 1,
  }
  ```
  A domain pushes its maps in its flow presenter's `Start` and disposes the handle in `Dispose`. Nested domains (Pause) push `Ui` on top of Gameplay's `Player | Ui`. Popping restores the parent's set.
- Rebinding: `GameInput.asset.SaveBindingOverridesAsJson()` / `LoadBindingOverridesFromJson`, persisted by `ISettingsService` (§10.4).

### 9.2 Input locking (MLock)

- Core defines the app-wide tag enum:
  ```csharp
  [Flags]
  public enum InputLockTag
  {
      None = 0,
      Movement = 1 << 0,
      Camera = 1 << 1,
      Interaction = 1 << 2,
      Ui = 1 << 3,
      Pause = 1 << 4,
  }
  ```
  New categories are added here, in Core. The enum is shared by all domains on purpose.
- Root registration: `builder.RegisterInstance<ILockService<InputLockTag>>(new BaseLockService<InputLockTag>())`.
- Input handlers implement `ILockable<InputLockTag>`:
  - `LockTags` returns the tags they belong to.
  - They `Subscribe(this)` in `Start` and `Unsubscribe(this)` in `Dispose`.
  - `HandleLocking()` sets `_isLocked = true` and zeroes cached continuous values. `HandleUnlocking()` clears the flag.
  - Callbacks return early while locked.
- Lockers (cutscenes, dialogs, transitions, pause):
  ```csharp
  using var inputLock = _locks.Lock(InputLockTag.Movement | InputLockTag.Camera);
  await PlayCutsceneAsync(ct);
  ```
  Use `LockAll()` / `LockAllExcept(...)` as needed. A lock MUST be disposed on every path (`using`).
- Domains MAY create their own `BaseLockService<TheirTag>` for domain-local features, registered in their scope.
- `SceneTransitionService` holds `LockAll()` while a transition overlay is visible.

### 9.3 Time

```csharp
public interface ITimeService
{
    ReadOnlyReactiveProperty<bool> IsPaused { get; }
    IDisposable Pause();                 // ref-counted; paused while any handle is alive
    float TimeScale { get; set; }        // applied to Time.timeScale while not paused
}

public interface IClock { DateTime UtcNow { get; } }
public interface IRealClock : IClock { }   // wall-clock UTC, unscaled, runs during pause; seam for future server time
public interface IGameClock : IClock { }   // starts at boot, advances by scaled delta time, stops while paused

public interface ITimerService
{
    ITickSource Real { get; }
    ITickSource Game { get; }
}

public interface ITickSource
{
    Observable<Unit> EverySecond { get; }
    Observable<Unit> EveryMinute { get; }
    ReadOnlyReactiveProperty<TimeSpan> CountdownTo(DateTime utcEnd);   // clamps at zero; emits on the aligned second tick
}
```

- One `ITickable` inside `TimerService` checks each clock for whole-second and whole-minute boundaries and emits then. Every timer in the game therefore updates **in the same frame**.
- Game-time countdowns (level timer) use `Game`. Cooldowns that persist across sessions (daily rewards) use `Real`, and store `DateTime` UTC end times in saves.
- No server time and no anti-cheat. `IRealClock` is the seam for them.

### 9.4 Audio

- One `AudioMixer` (in `CoreConfig`) with groups `Master` → `Music`, `Sfx`, `Ui`. Exposed volume parameters are bound to settings.
- `AudioCue` ScriptableObject: `AudioClip[] Clips` (random pick), `AudioMixerGroup Group`, `float Volume`, `Vector2 PitchRange`, `bool Loop`. Cues live in the domain that owns them. Clips referenced from cues MAY be `Loadable<AudioClip>` if memory matters (default: direct references, since cues are in the domain's content directory anyway).
  ```csharp
  public interface IAudioService
  {
      void Play(AudioCue cue);
      void PlayAt(AudioCue cue, Vector3 position);
      void PlayAttached(AudioCue cue, Transform target);
      UniTask PlayMusicAsync(AudioCue cue, float crossfadeSeconds, CancellationToken ct);
      UniTask StopMusicAsync(float fadeSeconds, CancellationToken ct);
  }
  ```
- SFX use a pool of `AudioSource`s under the root prefab. 2D vs 3D is decided by the call (`Play` vs `PlayAt`/`PlayAttached`).

### 9.5 Scene transitions

```csharp
public interface ISceneTransitionService
{
    UniTask ShowAsync(Transition transition, CancellationToken ct);  // no-op for Transition.None
    UniTask HideAsync(Transition transition, CancellationToken ct);
}
```

The overlay (`TransitionOverlayView`, a full-screen canvas with a `CanvasGroup`) lives on the root prefab. Nested or parallel `Show` calls are ref-counted. While visible, the service holds an `InputLockTag` `LockAll()`. The caller of `RunAsync` chooses the transition (§4.3). There is no automatic fade.

### 9.6 Cameras (Cinemachine 3)

- The single `Camera` + `CinemachineBrain` lives on the root prefab and persists.
- `CinemachineCamera`s (virtual cameras) live in domain scope or content scenes, and domains control them through views. For example `GameplayCameraView` exposes `SetFollowTarget(Transform)` and `SetPriority(int)`. Presenters decide *when*.
- Blends are configured on the brain (default blend) or in `CinemachineBlenderSettings` assets owned by domains.
- UI canvases in domain scenes use `Screen Space - Overlay`, or `Screen Space - Camera` with the root camera assigned by the view in `Awake` via `Camera.main`. This is the one allowed lookup (§16).

---

## 10. Content and data

### 10.1 Content Directories

Facts verified from the Unity 6.6 scripting API (`Unity.Loading`, `UnityEngine.ContentLoadModule`):

- Build (Editor): `BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters { name, outputPath, rootAssetPaths, compression, options, extraScriptingDefines })`.
- Runtime registration: `ContentDirectoryHandle ContentLoadManager.RegisterContentDirectory(string localPath)` and `ContentLoadManager.UnregisterContentDirectory(handle)`. `GetRootAssets<T>(handle)` returns the root assets of a type.
- Asset references: `Loadable<T>` (a serialized reference). `LoadAsync()` returns an awaitable that yields `T`, or **`null` on failure**. `Release()` lets Unity unload it. `Status` (`LoadableStatus.None/Loading/Loaded/Failed`), `Target`.
- Scenes: `LoadableSceneId` (a stable serialized scene id), `SceneManager.LoadSceneAsync(LoadableSceneId, LoadSceneParameters)` returning `AsyncOperation`, and `SceneManager.GetSceneByLoadableSceneId(id)`.

Design:

- **One content directory per domain**, whose root asset is `<Name>Content.asset`. It lists the scope scene, content scenes, and root prefabs and configs. Core's own assets (overlay, mixer, `CoreConfig`) are referenced directly by the root prefab and are part of the player build.
- `ContentDirectoryRegistry` (Core) registers every built content directory at boot. The output path is under `StreamingAssets/Content/<Name>` (§15.1). It exposes `OneOf<ContentDirectoryHandle, NotFound> Get(string name)`. Domains don't register or unregister directories themselves. Memory is managed per asset through `Release()`, and scenes through unloading.
- An Editor build step (`Core.Editor`, menu `Build/Content Directories` + a build preprocessor) builds every `<Name>Content.asset` found under `Assets/_Project/Domains/`.
- All loading is **async**:
  ```csharp
  public interface IContentLoader
  {
      UniTask<OneOf<T, NotFound>> LoadAsync<T>(Loadable<T> loadable, CancellationToken ct) where T : UnityEngine.Object;
      void Release<T>(Loadable<T> loadable) where T : UnityEngine.Object;
  }

  public interface ISceneLoader
  {
      UniTask<OneOf<Scene, NotFound>> LoadAdditiveAsync(LoadableSceneId id, CancellationToken ct);
      UniTask UnloadAsync(Scene scene, CancellationToken ct);
  }
  ```
  `null` from `LoadAsync` maps to `NotFound`. A missing **scope scene** is a configuration bug, so `DomainRunner` throws.
- Configs reference heavy or optional assets through `Loadable<T>` fields, so they load only when needed and are released by the presenter/service that loaded them (in `Dispose`).
- Synchronous `Loadable<T>.Load()` is **forbidden**. Async only.
- `Resources.Load` and Addressables are **forbidden**.

### 10.2 Configuration data

- **Default (≈99%): ScriptableObject configs.** They live in the domain's `Configs/`, are referenced from the domain's `LifetimeScope` (or its descriptor), and are registered with `RegisterInstance`. Presenters and services receive them through constructor injection as plain objects.
- Config SOs are **read-only at runtime**. Never write to them.
- Odin attributes are welcome for validation (`[Required]`, `[MinValue]`, `[ValidateInput]`) and editor UX.
- JSON data files (through `IJsonSerializer`) only for large tabular data that is impractical as SOs. This needs a reason stated in the PR.

### 10.3 Save data

- One JSON file per slot: `{persistentDataPath}/Saves/slot_{index}.json`, written atomically.
- File shape:
  ```json
  {
    "formatVersion": 1,
    "savedAtUtc": "2026-09-28T12:00:00Z",
    "sections": {
      "gameplay": { "version": 2, "data": { } },
      "mainMenu": { "version": 1, "data": { } }
    }
  }
  ```
- Each domain owns **its own section**, under a string key equal to the domain name in camelCase. The section holds a versioned **DTO** (a `sealed record` or `[Serializable]` class with public get/init properties, no Unity types except via primitive fields). DTOs are separate from runtime models. Mapping is explicit code.
- Deleting a domain leaves an orphaned section that nothing reads. That is harmless and intentional.
- Migration: each section declares `const int CurrentVersion` and a `Migrate(JObject data, int fromVersion) → OneOf<JObject, Corrupted>` function, run on load when `version < CurrentVersion`.
  ```csharp
  public interface ISaveStore
  {
      int ActiveSlot { get; }
      UniTask<OneOf<Success, Error>> SelectSlotAsync(int slot, CancellationToken ct);          // loads the file into memory
      OneOf<T, NotFound, Corrupted> Read<T>(SaveSection<T> section) where T : class;             // from memory
      void Write<T>(SaveSection<T> section, T data) where T : class;                             // to memory
      UniTask<OneOf<Success, Error>> FlushAsync(CancellationToken ct);                          // to disk
      UniTask<OneOf<Success, Error>> DeleteSlotAsync(int slot, CancellationToken ct);
  }

  public sealed record SaveSection<T>(string Key, int CurrentVersion, Func<JObject, int, OneOf<JObject, Corrupted>> Migrate) where T : class;
  ```
  Reads and writes are in memory. `FlushAsync` persists. Domains flush at meaningful points (level end, quitting to menu). Core flushes on application quit/pause.

### 10.4 Settings

- Separate file `{persistentDataPath}/settings.json`, not tied to a slot. Same storage adapters.
- Contents: master/music/sfx/ui volumes, language, graphics (quality level, fullscreen mode, resolution, vsync), input binding overrides JSON.
- ```csharp
  public interface ISettingsService
  {
      ReadOnlyReactiveProperty<SettingsState> Current { get; }
      void Apply(SettingsState state);                               // applies side effects immediately (mixer, quality, language, bindings)
      UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct);
  }

  public sealed record SettingsState(float MasterVolume, float MusicVolume, float SfxVolume, float UiVolume, Language Language, int QualityLevel, FullScreenMode FullScreenMode, Resolution Resolution, bool VSync, string BindingOverridesJson);
  ```
- Loaded during Core startup, before `GameFlow` starts. On `NotFound`/`Corrupted`: log (handled here), use defaults from `CoreConfig`, and save.

### 10.5 Localization (custom; Unity Localization is not used)

- `Language` enum in Core (`English = 1`, ... ; `0` reserved). `CoreConfig` lists the supported languages and the default.
- **Tables:** one `LocalizationTable` ScriptableObject **per domain** (`<Name>Text.asset`, Odin-edited: rows = keys, columns = languages). Shared strings live in a `Shared` table. The table is registered in the domain's scope, and the auto-registered `LocalizationTableRegistration` entry point adds it to the service on start and removes it on dispose. Tables load and unload with their domain.
- **Keys:** generated constants. `Core.Editor` has a generator (menu + on-table-save) that emits `<Name>Text.g.cs` next to the table:
  ```csharp
  // <auto-generated/>
  namespace Gameplay
  {
      internal static class GameplayText
      {
          public static readonly TextKey WinTitle = new("Gameplay", "win_title");
      }
  }
  ```
  A typo is then a compile error, not a silent miss.
- Service:
  ```csharp
  public interface ILocalizationService
  {
      ReadOnlyReactiveProperty<Language> Current { get; }
      string Get(TextKey key);                         // missing → returns "{table}/{key}" and logs Warn once per key
      string Format(TextKey key, params object[] args);
      void SetLanguage(Language language);             // called by SettingsService.Apply
  }
  ```
- **Static labels:** `LocalizedLabel` (a Core MonoBehaviour, i.e. a view) holds `[SerializeField] TextKey _key` + `[SerializeField, Required] TMP_Text _text`, and exposes `TextKey Key` and `SetText(string)`. It has no logic and no injection.
- **Binder:** `LocalizedLabelBinder` is an entry point that `DomainRunner` auto-registers in every domain scope. On start it collects every `LocalizedLabel` in the domain's scope scene (and in content scenes as `DomainSceneSet` loads them), sets their text, and re-sets it whenever `Current` changes. It disposes its subscription with the scope. The root scope has its own binder for root-prefab labels.
- **Dynamic text** is set by presenters through `ILocalizationService`, re-applied when `Current` changes.
- Fonts: one TMP font asset with fallbacks covering the supported languages. It lives in `Shared/`.

---

## 11. Coding rules summary (additions to `Docs/Coding Conventions.md`)

- Conventions file: Allman braces, `_camelCase` private fields, member ordering as specified, `[SerializeField] private` or `[field: SerializeField]` properties, no public fields, enums with explicit values and `0 = None`.
- Classes are `sealed` by default. Domain internals are `internal`.
- DI classes use **explicit constructors** that assign `private readonly` fields (matches the conventions' field naming). **Primary constructors are only for records** (§16).
- `var` per the conventions (use it when the type is obvious).
- File-scoped namespaces are allowed. The conventions sample uses block namespaces. **Pick file-scoped for all new code** (§16).
- No LINQ in per-frame code (`ITickable` paths). LINQ is fine elsewhere.
- No `static` mutable state except `Log` configuration and `BootMode`.
- Every `IDisposable` created is disposed by its owner. Every subscription lands in a `DisposableBag`.

---

## 12. Lifecycle walkthrough (normal boot)

1. Unity loads `Bootstrap.unity`. VContainer instantiates the `RootLifetimeScope` prefab (via `VContainerSettings`) before any scene scope.
2. `BootMode.Current` = `Normal`. Root `Configure` runs `CoreInstaller` and registers the domain entries plus `GameFlow`.
3. Core startup entry points run: `ContentDirectoryRegistry` registers directories, `SettingsService` loads and applies settings, `SaveStore` selects slot 0 (lazily or here, §16), and `LocalizationService` sets the language.
4. `GameFlow.StartAsync(ct)` calls `MainMenuDomain.RunAsync(args, Fade, ct)`, and `DomainRunner` then:
   1. shows the fade and takes the load gate,
   2. enqueues the root as parent plus the args, completion, and binder,
   3. loads `MainMenu.unity` from the MainMenu content directory; `MainMenuLifetimeScope` builds as a child of root,
   4. releases the gate, hides the fade, and awaits the completion.
5. The user clicks Play. `MainMenuPresenter` calls `Complete(new MainMenuResult.Play())`. The runner's `finally` disposes the scope (presenters dispose, subscriptions end, input handles pop) and unloads the scene. `RunAsync` returns `Play`.
6. `GameFlow` runs `GameplayDomain.RunAsync(...)`, and so on.

---

## 13. Sample vertical slice (ships with the template; deletable)

Purpose: every pattern in this document exercised once, as small as possible.

| Domain | Kind | Content | Demonstrates |
|---|---|---|---|
| `MainMenu` | main | Canvas with Play / Settings / Quit, version label | view outputs through R3, `DomainCompletion`, launching a leaf domain, localization labels |
| `Gameplay` | main | a room (content scene `Gameplay_Room`), a capsule player, Cinemachine follow camera, N collectibles, HUD (score + countdown), win/lose panel | content scenes, input handlers + `IFixedTickable` movement, MLock, `TimerService` countdown (game clock), `ScoreModel` with `ReadOnlyReactiveProperty`, save section (best score), `AudioCue`s, `ViewPool` (collectible pickup VFX or HUD popups) |
| `Gameplay/Pause` | sub | overlay: Resume / Settings / Quit to menu | sub-domain in the parent's assembly, `ITimeService.Pause()`, input map stack push, returning a union to the parent (`Resume`, `OpenSettings`, `QuitToMenu`). Because of the depth limit, **Pause returns `OpenSettings` and Gameplay launches Settings**, then re-runs Pause. |
| `Settings` | leaf | overlay: volume sliders, language dropdown, back | leaf domain reused from MainMenu (depth 2) and Gameplay (depth 2), `ISettingsService`, localization language switch |

Results: `MainMenuResult = Play | Quit`. `GameplayResult = Won | Lost | QuitToMenu`. `PauseResult = Resume | OpenSettings | QuitToMenu`. `SettingsResult = Closed`.

A parallel long-lived domain is **not** in the sample. §4.7 documents the pattern.

---

## 14. Testing

### 14.1 Libraries

- NUnit (Unity Test Framework 1.8), **NSubstitute 6.2** (Editor only; uses Reflection.Emit, fine in EditMode), **AwesomeAssertions 9.6** (`using AwesomeAssertions;`, API as FluentAssertions 7: `x.Should().Be(...)`).
- These DLLs are `autoReferenced=false`. Test asmdefs reference them explicitly (§3.3). Runtime asmdefs MUST NOT reference them. Their plugin importers SHOULD be set to Editor-only so they never reach player builds (§15.4).

### 14.2 What to test

- **EditMode, per assembly** (`Core.Tests`, `Gameplay.Tests`, …): services, models, union-returning logic, save migrations, JSON round-trips (against `IFileStorage` substitutes), `TimerService` alignment (with fake clocks), `DomainCompletion`, and localization lookup.
- Presenters: only when they carry real logic. In that case introduce a view interface for that presenter (§6.1) and substitute it.
- Objects under test are **constructed by hand**, never through a VContainer container.
- **PlayMode smoke test** (`Bootstrap.PlayModeTests`): boots the root, then for each `IDebugRunnableDomain` runs `RunDebugAsync` with a linked token. It asserts that the scope scene loaded and that the scope built, cancels, and asserts that the scene unloaded and the scope was disposed.

### 14.3 Style

- Class `<Subject>Tests`. Method `MethodName_Condition_ExpectedResult`. Body sections `// Arrange`, `// Act`, `// Assert`.
- Async tests are `public async Task Name()` (UTF 1.8 supports them). Inside, `await` UniTasks directly or via `.AsTask()`.
- Union assertions come from `TestUtils`:
  ```csharp
  result.Should().BeCase<NotFound>();
  var won = result.Should().BeCase<GameplayResult.Won>().Which;
  won.Score.Should().Be(10);
  ```
  Implement these as AwesomeAssertions extensions over `IOneOf` (`.Value`, `.Index`).
- Fakes: prefer NSubstitute. Hand-written fakes (`FakeClock`) go in `TestUtils` when they are reused.

### 14.4 Not in scope now

Architecture-rule tests (asmdef reference validation), CI, and AI-agent tooling (CLAUDE.md, scaffolders, the Unity CLI/`com.unity.pipeline` workflow) are **deferred** to a separate design session. Do not implement them.

---

## 15. Unverified assumptions — spike these first

| # | Assumption | How to verify | Fallback |
|---|---|---|---|
| 15.1 | Content Directories: (a) scenes loaded via `LoadableSceneId` need no Build Settings entry; (b) in the Editor, `Loadable<T>`/`LoadableSceneId` load without building directories (or the build must run before Play); (c) where the built output must live for players (`StreamingAssets/Content/<Name>` assumed) and when to call `RegisterContentDirectory`; (d) whether `LoadableSceneId` can be authored from a `SceneAsset` in the inspector; (e) how `Loadable<T>.LoadAsync`'s awaitable converts to UniTask (UniTask supports custom awaitables via `await`; wrap if needed); (f) whether `LifetimeScope.EnqueueParent` works with scenes loaded via `LoadSceneAsync(LoadableSceneId, ...)` (it should, since it hooks `Awake`). | Spike in a throwaway domain: build, register, load a scene and a prefab, in Editor and in a standalone build. | Keep the `IContentLoader`/`ISceneLoader` interfaces and adapt the implementations only. Nothing else may change. |
| 15.2 | `OneOf.SourceGenerator` is picked up as a Roslyn analyzer by NuGetForUnity (label `RoslynAnalyzer`) and works on 6.6 with `-langversion:12`. | Declare one `[GenerateOneOf]` union in Core and compile. | Hand-written named unions (§7.2). |
| 15.3 | MLock 2.1.0 compiles on 6.6 and its debug windows work. | Open the project. | Fix in the submodule, PR upstream. |
| 15.4 | ✅ *Verified 2026-09-28: after listing transitive deps explicitly, restore succeeded and the project compiles; no Assembly Version Validation change was needed.* Remaining: NuGet restore of R3's dependencies (`Microsoft.Bcl.TimeProvider`, `System.Threading.Channels`, …) causes no version conflicts. | Open the project and check the console. | Disable Player Settings → "Assembly Version Validation" (recommended by R3). Set NSubstitute/Castle/AwesomeAssertions importers to Editor-only. |
| 15.5 | Per-asmdef `csc.rsp` with `-langversion:12 -nullable:enable` is honored by Unity 6.6's compiler and reflected in Rider's generated csproj. | Compile a file using a collection expression and a nullable warning. | — |
| 15.6 | `readonly record struct` `LogTag` serializes in Unity. | Inspector check. | Plain `[Serializable] struct` (§7.7). |
| 15.7 | R3 `SubscribeAwait` + UniTask interop shape (§6.3). | Compile the sample presenter. | `async UniTaskVoid` handler with a busy flag. |

The first implementation task is to **run these spikes and update this document** with the verified facts.

---

## 16. Derived rules (decided by the architect, not explicitly discussed with the owner — owner may override)

1. DI classes use explicit constructors with `private readonly _fields`. Primary constructors are only for records (reason: the conventions' field naming and ordering).
2. File-scoped namespaces for new code (the conventions sample shows block namespaces; the conventions don't forbid either).
3. Plain C# `event`s are not used; R3 everywhere (owner approved "R3 for anything subscribable", and this is the literal reading).
4. The project-wide Input Actions reference in Input System settings is removed in favour of the generated `GameInput` instance, to avoid two copies of the actions.
5. `Camera.main` is allowed only in views, for assigning a canvas's world camera.
6. Pause cannot launch Settings (depth limit). It returns `OpenSettings` to Gameplay instead.
7. The save slot is selected at boot (slot 0) by Core. A slot-selection UI is out of scope.
8. `SubscribeAwait` vs `UniTaskVoid` handler pattern: one is chosen during the §15.7 spike and used consistently.
9. `LogTag` lives on descriptors, so the runner can tag its own logs per domain.
10. `IApplicationService` wraps `Application.Quit`/version for testability.
11. Domain internals are `internal`. Only the entry class, args, result, and descriptor are public.

---

## 17. Decision log (from the design interview, 2026-09-28)

| # | Decision | Rationale / owner note |
|---|---|---|
| D1 | Genre-agnostic, 3D, URP template with a tiny deletable sample slice; desktop platforms. | Agents learn best from a working example. |
| D2 | A domain = a feature with its own lifetime. App-lifetime infrastructure goes in Core. Default one asmdef per domain. | Avoid granular domains (P3). |
| D3 | Root scope via the VContainerSettings root prefab. Domain scenes are additive. Play from any scene. | Composable domains; fast iteration. |
| D4 | Domains don't talk. The launcher awaits `RunAsync` and gets a union result. Escape hatch: Core contracts implemented by domains and registered by Bootstrap. | Linear readable flow; deletion breaks only call sites. No message bus. |
| D5 | MVP: passive MonoBehaviour views with R3 outputs and imperative inputs; plain C# presenters as entry points. | Owner: "inject MonoBehaviours, not into them". |
| D6 | Unions for expected failures, exceptions for bugs, cancellation stays exception-based, try/catch only at edges, domain-specific error types. | P8 without fighting UniTask. |
| D7 | Odin for the inspector (+ serializer sparingly); Newtonsoft for persistence. | Human- and agent-readable saves. |
| D8 | ScriptableObject configs registered into scopes (~99%). JSON config files only very rarely. | Owner: "c should be really rare". |
| D9 | AI tooling (CLAUDE.md, scaffolders, rule tests, Unity CLI) deferred to a separate session. | Owner. |
| D10 | Unity 6.6 + Content Directories; async loading only. | Owner upgraded. |
| D11 | Private repo; Odin committed. | Owner. |
| D12 | NuGetForUnity, packages in `Packages/nuget-packages`; restored on open (not committed). | Owner. |
| D13 | Don't touch `com.unity.pipeline` or the other default packages now. | Owner. |
| D14 | Top-level flow assembly named **Bootstrap**, referencing all domains. | Owner named it. |
| D15 | Parallel domains allowed; one instance per domain type; long-lived domains end through cancellation. | Owner: "multiple domains can be loaded simultaneously". |
| D16 | Nesting max root → main → sub; enforced. Sub-domains live in the parent's assembly. Shared leaf domains get their own asmdef, depending only on Core/Shared. | Owner: "Core → MainDomain → SubDomain is far enough". |
| D17 | Debug play of a domain scene logs the result and exits Play mode. | — |
| D18 | Bare namespaces, `Assets/_Project`, per-domain art. | — |
| D19 | Core services: DomainRunner, Content, Input, Save, Settings, Audio, Transitions, Time (pause/scale, real+game clocks, aligned TimerService), Localization (custom), Input Locking via MLock (submodule). | Owner added TimerService, Localization, MLock. |
| D20 | Input via generated callback interfaces; `ITickable` only to apply continuous values; map stack. | Owner prefers the interfaces over polling. |
| D21 | R3 for everything subscribable; short chains. | — |
| D22 | Lifetime and cancellation rules (§5.5). | — |
| D23 | Concrete views; interfaces only for complex, tested presenters. | — |
| D24 | Dynamic views: one presenter drives many; pooled `ViewFactory` from day one. | — |
| D25 | One content directory per domain. | Deletability + memory lifetime. |
| D26 | Exactly one scope scene per domain; other scenes are content-only. | — |
| D27 | Save: one JSON file per slot, per-domain versioned sections, DTOs. | — |
| D28 | Static `Log` with tags; log where handled. | — |
| D29 | C# 12 via per-asmdef `csc.rsp`, nullable enabled, no compiler patch. | Owner: 6.6's bundled compiler handles C# 12. |
| D30 | Only polyfill: `IsExternalInit`. | Owner chose (b). |
| D31 | Named unions (`[GenerateOneOf]`) for public API, inline for private. | — |
| D32 | Tests: NSubstitute + AwesomeAssertions (FA 8 is commercial); EditMode per assembly, hand-built objects, one PlayMode smoke test, `async Task` tests, union-assert helpers. | Owner chose AwesomeAssertions. |
| D33 | uGUI + TextMeshPro for views. | — |
| D34 | `RootLifetimeScope` in Bootstrap; boot mode decides `GameFlow` vs `DebugDomainBoot`; only `Bootstrap.unity` in Build Settings. | — |
| D35 | Transitions chosen per `RunAsync` call; default none. | Parallel domains must not all fade. |
| D36 | Audio: mixer groups, `AudioCue` SOs per domain, pooled sources, music crossfade. | — |
| D37 | Settings in their own `settings.json` (including rebinds). | — |
| D38 | `= null!` + Odin `[Required]` for serialized references. | — |
| D39 | Dependencies installed now; implementation deferred until the AI-tooling session closes. | Owner. |

---

## 18. Suggested implementation order (for the orchestrator)

1. **Spikes (§15)**, then update this document.
2. Core foundations: polyfill, `Results`, `Log`, storage adapters + tests.
3. Domains: `DomainRunner`, `DomainLifetimeScope`, `ScopeRef`, `DomainCompletion`, `DomainSceneSet`, `RegisterDomain`, plus tests for the guards and completion.
4. Bootstrap: root prefab, `VContainerSettings`, `CoreInstaller` skeleton, `BootMode`, `GameFlow`, the play-from-any-scene editor hook.
5. Content: `ContentDirectoryRegistry`, loaders, the editor build step.
6. Core services in parallel (independent): Input + locking, Time, Audio, Transitions, Save, Settings, Localization (+ key generator), Views/pooling.
7. Shared.UI + TestUtils.
8. Sample domains: Settings (leaf) → MainMenu → Gameplay (+ Pause).
9. PlayMode smoke test.
10. Final pass: every `csc.rsp` present, asmdef references match §3.3, no forbidden APIs (`Resources.Load`, `Debug.Log`, `[Inject]`, `async void`, `event`, sync `Load()`).
