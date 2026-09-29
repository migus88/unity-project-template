# Architecture Specification

> **Audience:** AI agents (an orchestrator and the implementation agents it spawns). This is not a human tutorial.
> **Status:** Implemented. This document describes the code under `Assets/_Project/` as of 2026-09-29. A change to a public shape or to a rule MUST update this document in the same change.
> **Normative language:** **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, **MAY** follow RFC 2119. A MUST rule may only be broken by a human decision recorded in §17 (Decision Log).
> **Companion documents:** `Docs/Rules.md` (standing owner rules: no runtime object creation, no comments in code, code layout, commits, persisted data) and `Docs/Coding Conventions.md` (naming, formatting, member ordering, serialization). Both are binding. `Docs/Rules.md` overrides this document and the conventions where they conflict. Where this document and the conventions conflict, this document wins and the conflict is listed in §16.

---

## 0. How to use this document

1. Read the whole file before planning. Sections refer to each other.
2. §1–§3 give the tooling and the layout of the project. §4–§6 give the runtime model: domains, dependency injection, presentation. §7 covers errors. §8–§10 cover the Core services. §11 lists coding rules. §12 walks through a boot. §13 is the sample game. §14 covers testing. §15 records the verified spike results. §16 lists rules this document derived that the owner never discussed. §17 is the decision log.
3. Code blocks are **reference skeletons**. They fix names, shapes, and responsibilities. Bodies are indicative. They follow `Docs/Coding Conventions.md` member ordering and contain no comments (`Docs/Rules.md` §2).
4. New work follows the existing code: copy the shape of the nearest existing domain or Core service.

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
| P10 | Everything that exists at runtime is authored. | No runtime object creation (`Docs/Rules.md` §1, §6.6). Variable counts are fixed authored sets sized for the worst case. |

---

## 2. Tooling

### 2.1 Engine

- Unity **6000.6.3f1** (Unity 6.6). URP 17.6. 3D.
- Target platforms: **Windows, macOS, Linux** (standalone). Mobile and WebGL are out of scope. Do not add workarounds for them.
- Scripting backend: whatever is set in the project (unchanged). Nothing in this design may rely on Reflection.Emit at runtime in player builds. NSubstitute is Editor-only, see §14.

### 2.2 Packages

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
| Input System | 1.20.0 | Unity registry | `Unity.InputSystem` |
| MLock | 2.1.0 | git submodule `Submodules/MLock`, referenced via `file:` in `Packages/manifest.json` | `MLock.Runtime`, ns `Migs.MLock`, `Migs.MLock.Interfaces` |
| Odin Inspector & Serializer | 4.x | committed in `Assets/Plugins/Sirenix` (the repo is private) | `Sirenix.OdinInspector`, `Sirenix.Serialization` |
| TextMeshPro / uGUI | ugui 2.6.0 | Unity registry; TMP Essentials committed in `Assets/TextMesh Pro/` | `Unity.TextMeshPro`, `UnityEngine.UI` |
| Test Framework | 1.8.0 | Unity registry | NUnit |
| NSubstitute | 6.2.0 | NuGet, `autoReferenced=false` | tests only |
| AwesomeAssertions | 9.6.0 | NuGet, `autoReferenced=false` | tests only (Apache-2.0 fork of FluentAssertions; FA 8+ is commercially licensed and **MUST NOT** be used) |
| NuGetForUnity | 4.5.0 | OpenUPM | editor tool |

Package rules:

- NuGet packages live in `Packages/nuget-packages/`. `packages.config` is committed. `InstalledPackages/` is **gitignored** and restored when Unity opens.
- **NuGetForUnity restore installs only what `packages.config` lists — transitive dependencies MUST be listed explicitly** (without `manuallyInstalled`). Test-only packages and their dependencies (NSubstitute, Castle.Core, System.Diagnostics.EventLog, System.Security.Principal.Windows, AwesomeAssertions) are `autoReferenced="false"`. When adding a NuGet package, resolve its dependency closure for .NET Standard 2.1 (fall back to 2.0) and list it all.
- `Core.Editor.TestOnlyPluginImporterEnforcer` makes the test-only DLLs Editor-only after every restore, so they never reach player builds (§15.4).
- MLock is a submodule so the owner can edit it in place and upstream the changes with a PR. Agents MAY change MLock when needed. Such changes MUST be committed inside the submodule and flagged to the owner. They MUST NOT be copied into `Assets/`.
- `com.unity.pipeline`, `com.unity.visualscripting`, `com.unity.multiplayer.center` and `com.unity.collab-proxy` MUST NOT be removed or reconfigured (`Docs/Rules.md` §6).
- Do not add packages that are not listed here without owner approval. Explicitly **not** used: Addressables, Unity Localization (it depends on Addressables), MessagePipe, DOTween, Zenject/Extenject, UniRx, FluentAssertions ≥ 8.

### 2.3 C# language

- Every first-party asmdef (everything under `Assets/_Project/`) has a sibling `csc.rsp`:
  ```
  -langversion:12
  -nullable:enable
  ```
  Do **not** place a root `Assets/csc.rsp`. That would apply to third-party code. A sub-domain's `.asmref` folder has no `csc.rsp`: its scripts compile with the main assembly's (§3.2).
- **No compiler patching** (unity-csharp-patch was evaluated and rejected). C# 13/14 features are unavailable: no `field` keyword, no extension members, no `params` collections.
- **Exactly one polyfill is allowed:** `IsExternalInit`, so that `record class` and `init` work. It lives in `Core/Code/Polyfills/IsExternalInit.cs`:
  ```csharp
  namespace System.Runtime.CompilerServices
  {
      public static class IsExternalInit
      {
      }
  }
  ```
  `required` members, `CallerArgumentExpression`, interpolated string handlers, and generic attributes are **forbidden** (they need extra polyfills or crash on Mono). Ref fields, static abstract interface members, and inline arrays are unsupported on this runtime.
- **Namespaces are block-scoped** (`namespace X { ... }`). File-scoped namespaces compile, but Unity 6.6 then fails to associate the `MonoScript` with its class (`MonoScript.GetClass()` returns null; components and ScriptableObjects serialize with a broken `m_Script`). Verified in §15.5.
- Allowed and encouraged: `record` / `readonly record struct`, `init`, switch expressions, pattern and list patterns, collection expressions, raw string literals, `global using` (only inside an assembly's own `GlobalUsings.cs`, and sparingly).
- **Nullable reference types are enabled.**
  - Injected constructor parameters are non-nullable. Constructors do not null-check them. VContainer guarantees resolution or throws at build time.
  - Unity-serialized references on MonoBehaviours and ScriptableObjects: `[SerializeField, Required] private Button _playButton = null!;`. The `null!` means "Unity assigns this". `[Required]` is Odin's attribute, so a missing reference shows up in the inspector and validator.
  - "Might be missing" in an API is expressed as a union (`OneOf<T, NotFound>`), **not** as `T?` returned from public service methods. `T?` is fine for private state and local variables.

### 2.4 Odin vs Newtonsoft vs Unity serialization

| Concern | Tool |
|---|---|
| Inspector UX (validation, grouping, buttons, tables) | Odin Inspector. Use its attributes freely in runtime code, since Odin is committed. |
| Config ScriptableObjects that need interfaces, dictionaries, or polymorphism | `SerializedScriptableObject` (Odin Serializer). Use it sparingly. Plain `ScriptableObject` + `[SerializeReference]` is preferred when it suffices. |
| Runtime persistence (saves, settings) and JSON data files | Newtonsoft.Json, only through Core's `IJsonSerializer` adapter (§7.6, §10.3). |
| Everything else | Unity serialization. |

Never use Odin Serializer or `JsonUtility` for save data. Never serialize live runtime objects. Serialize DTOs only.

---

## 3. Project structure

### 3.1 Folder layout

Every module keeps its runtime and Editor scripts, asmdefs and `csc.rsp` files in `<Module>/Code/`, its test assembly (asmdef, `csc.rsp`, tests) in `<Module>/Tests/`, and its non-code assets (scenes, prefabs, configs, art) outside `Code/`. `Shared/TestUtils` is a test-helper assembly with its files at its root (`Docs/Rules.md` §4).

```
Assets/
  _Project/                              ← all first-party content (underscore sorts it first)
    Core/
      Code/
        Core.asmdef                      rootNamespace: Core
        csc.rsp
        AssemblyInfo.cs                  InternalsVisibleTo("Core.Tests")
        CoreInstaller.cs                 registers every Core service into the root scope (§8)
        CoreConfig.cs                    root config ScriptableObject (§8)
        CoreStartup.cs                   settings load + save-slot selection (§8)
        IApplicationService.cs           + ApplicationService.cs
        LogTags.cs
        Polyfills/                       IsExternalInit
        Results/                         NotFound, Corrupted, Error (§7.3)
        Logging/                         Log, LogTag (§7.7)
        Domains/                         DomainRunner, DomainDescriptor, DomainContent, DomainLifetimeScope, DomainCompletion, DomainSceneSet, ScopeRef, Transition, RegisterDomain (§4)
        Content/                         ContentDirectoryRegistry, ContentLoader, SceneLoader (§10.1)
        Storage/                         FileStorage, JsonSerializer (§7.6)
        Save/                            SaveStore, SaveSection, SaveAutoFlush (§10.3)
        Settings/                        SettingsService, SettingsState, SettingsSection, graphics seam (§10.4)
        Input/                           GameInput.cs (generated), InputService, InputMaps, InputLockTag, InputBindingOverrides (§9.1, §9.2)
        Time/                            TimeService, clocks, TimerService (§9.3)
        Audio/                           AudioService, AudioCue, AudioSourceSet, AudioChannel (§9.4)
        Localization/                    LocalizationService, LocalizationTable, TextKey, LocalizedLabel, LocalizedLabelBinder (§10.5)
        Transitions/                     ILoadingScreen, NullLoadingScreen (§9.5)
        Editor/
          Core.Editor.asmdef             Editor-only: content build, text-key generator, test-plugin importer hook
          csc.rsp
      Input/GameInput.inputactions
      Audio/GameAudioMixer.mixer
      Tests/
        Core.Tests.asmdef                EditMode
        csc.rsp
    Bootstrap/
      Code/
        Bootstrap.asmdef                 rootNamespace: Bootstrap
        csc.rsp
        RootLifetimeScope.cs, GameFlow.cs, BootMode.cs, DebugDomainBoot.cs, EditorScopeScene.cs
        Editor/
          Bootstrap.Editor.asmdef        play-from-any-scene hook (§4.8)
          csc.rsp
      Prefabs/RootLifetimeScope.prefab   camera + CinemachineBrain, EventSystem, audio sources
      Settings/VContainerSettings.asset  RootLifetimeScope = the prefab above
      Settings/CoreConfig.asset
      Scenes/Bootstrap.unity             the ONLY scene in Build Settings
      Tests/
        Bootstrap.PlayModeTests.asmdef   smoke test (§14.2)
        csc.rsp
    Shared/
      UI/
        Code/
          Shared.UI.asmdef               reusable view widgets (no scope, no presenters of their own)
          csc.rsp
          Localization/SharedText.g.cs   generated keys of the Shared table (§10.5)
        Localization/SharedText.asset
        Prefabs/                         UICanvas, Button, Slider, Selector
        Fonts/                           TMP font assets (§10.5)
      TestUtils/
        TestUtils.asmdef                 test-only helpers (union assertions, fakes, test scopes)
        csc.rsp
    Domains/
      MainMenu/                          main domain
      Gameplay/                          main domain (contains the Pause sub-domain)
      Settings/                          leaf domain (settings overlay reused by MainMenu and Gameplay)
      Loading/                           main domain (loading screen, runs alongside the flow; implements ILoadingScreen, §9.5)
  TextMesh Pro/                          TMP Essentials (third-party, untouched)
  Plugins/Sirenix/                       Odin (third-party, untouched)
Submodules/MLock/                        git submodule
Packages/nuget-packages/                 NuGetForUnity
Docs/
```

A file in the Core root belongs to namespace `Core`. `IApplicationService` lives there because a `Core/Code/Application/` folder would create a `Core.Application` namespace that shadows `UnityEngine.Application` (§3.3).

### 3.2 Folder layout of a domain

```
Domains/<Name>/
  Code/
    <Name>.asmdef                        rootNamespace: <Name>
    csc.rsp
    <Name>Domain.cs                      entry class (§4.3)
    <Name>Args.cs                        args record
    <Name>Result.cs                      named union + its case types
    <Name>DomainDescriptor.cs            ScriptableObject type (§4.4)
    <Name>Content.cs                     DomainContent subclass (§4.4)
    <Name>LifetimeScope.cs               DomainLifetimeScope subclass (§5.3)
    <Name>Text.g.cs                      generated TextKey constants (§10.5)
    LogTags.cs
    AssemblyInfo.cs                      optional: InternalsVisibleTo("<Name>.Tests")
    <Feature>/                           presenters, services, models, views — by feature, not by kind
  <Name>DomainDescriptor.asset
  <Name>Content.asset                    content-directory root asset (§10.1)
  <Name>Text.asset                       localization table (§10.5)
  Scenes/<Name>.unity                    THE scope scene (exactly one)
  Scenes/<Name>_<Env>.unity              optional content-only scenes (no LifetimeScope)
  Configs/                               config assets
  Prefabs/                               prefabs placed into scenes in the Editor (never instantiated at runtime)
  Art/                                   meshes, textures, materials, animations, audio used only by this domain
  <Sub>/                                 sub-domain (§4.2)
    Code/
      <Name>.<Sub>.asmref                compiles into <Name>'s assembly; no asmdef, no csc.rsp
      <Sub>Domain.cs, <Sub>Args.cs, ...
    <Sub>DomainDescriptor.asset
    <Sub>Content.asset
    Scenes/<Sub>.unity
  Tests/
    <Name>.Tests.asmdef
    csc.rsp
```

- Assets used by exactly one domain MUST live inside that domain's folder. Deleting the folder removes everything. A sub-domain is self-contained in `<Name>/<Sub>/`: deleting that folder and its call site removes it.
- Assets used by more than one domain live in `Shared/` (for example `Shared/UI/`).
- Inside `Code/`, group by **feature** (`Code/Player/`, `Code/Hud/`), not by kind (`Presenters/`, `Views/`).

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
| `<X>.Tests` | `<X>`, its allowed references, `TestUtils`; `Core.Tests` also `Core.Editor` | — |
| `TestUtils` | `Core` | domains |

asmdef settings:

- Every first-party runtime asmdef: `"autoReferenced": false`, `"overrideReferences": false` (auto-referenced DLLs such as OneOf, R3 and Newtonsoft come for free), `rootNamespace` set, no `allowUnsafeCode`.
- Third-party references are added per assembly only when that assembly uses them. For example, only assemblies with views reference `Unity.TextMeshPro`.
- Test asmdefs:
  - `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"overrideReferences": true`.
  - `precompiledReferences`: `nunit.framework.dll`, `NSubstitute.dll`, `Castle.Core.dll`, `System.Diagnostics.EventLog.dll`, `System.Security.Principal.Windows.dll`, `AwesomeAssertions.dll`, plus whichever of `OneOf.dll`, `R3.dll`, `Newtonsoft.Json.dll` the tests use.
  - EditMode tests: `"includePlatforms": ["Editor"]`. The PlayMode smoke test has no `includePlatforms` and adds `UNITY_EDITOR` to its `defineConstraints`.
  - `TestUtils` has no `includePlatforms` (PlayMode tests may use it) and is constrained by `UNITY_INCLUDE_TESTS`.
- `InternalsVisibleTo` is allowed only towards the assembly's own test assembly (`Core` and `Core.Editor` → `Core.Tests`, `Gameplay` → `Gameplay.Tests`).

Namespaces:

- Bare: the assembly's root namespace plus the folder path below the module folder, with every `Code` segment skipped. `Core/Code/Save` → `Core.Save`. `Domains/Gameplay/Code/Flow` → `Gameplay.Flow`. `Domains/Gameplay/Pause/Code` → `Gameplay.Pause`. Tests: `Core/Tests/Save` → `Core.Tests.Save`. **No company or game prefix.**
- A folder whose namespace would shadow a Unity type or another first-party namespace MUST be renamed: `Cameras` (not `Camera`), `UserSettings` (not `Settings`, which is the Settings domain). The one existing exception is `Core.Time`: inside every `Core.*` namespace write `UnityEngine.Time`.

---

## 4. Domains

### 4.1 Definition

A **domain** is a feature with **its own lifetime**: it is created, lives for a while, and is torn down, usually together with scenes. Test for a new domain: *"Is there a moment this thing starts and a moment it ends, and does it own UI, scenes, or state for that span?"* If not, it belongs in Core (app-lifetime infrastructure) or inside an existing domain.

Each domain has:

- exactly one asmdef (sub-domains compile into their parent's through an asmref; see §4.2),
- exactly one **scope scene**, which holds its `LifetimeScope` and the views for its presenters,
- zero or more **content scenes** (environment, lighting, geometry), which have no `LifetimeScope`,
- one **content directory** (§10.1),
- one **entry class** plus `Args` and `Result` types.

Visibility: a main or leaf domain makes exactly its entry class, `Args`, `Result` and descriptor `public`. A sub-domain is used only inside its main domain's assembly, so all of its types are `internal`. **Everything else in the assembly is `internal`**, including the `<Name>Content` type.

### 4.2 Kinds and nesting

| Kind | Lives in | Launched by | Scope parent | May launch |
|---|---|---|---|---|
| **Main** | own asmdef | Bootstrap (`GameFlow`) | root | its sub-domains, leaf domains |
| **Sub** | `Domains/<Main>/<Sub>/`, with `<Sub>/Code/<Main>.<Sub>.asmref` compiling into the main assembly; namespace `<Main>.<Sub>` | only its main domain | the main domain's scope | nothing |
| **Leaf** | own asmdef, references only Core and Shared | Bootstrap or any main domain | the launcher's scope | nothing |

- Maximum depth: **root (0) → main (1) → sub or leaf (2)**. `DomainRunner` enforces it. A launch whose parent depth is ≥ 2 is a bug and **throws** `InvalidOperationException`.
- A sub-domain MAY resolve its parent's services through DI. That is the reason it shares the assembly.
- A leaf domain MUST only depend on Core and Shared, because it may be parented to different domains.
- Deleting a sub-domain means deleting its folder and its call site. Deleting a leaf or main domain means deleting its folder and fixing the compile errors in the launchers (Bootstrap or main domains).

### 4.3 Entry contract

Each domain exposes exactly one entry class:

```csharp
namespace Gameplay
{
    public sealed class GameplayDomain : IDebugRunnableDomain
    {
        public DomainDescriptor Descriptor => _descriptor;

        private readonly DomainRunner _runner;
        private readonly ScopeRef _launcherScope;
        private readonly GameplayDomainDescriptor _descriptor;

        public GameplayDomain(DomainRunner runner, ScopeRef launcherScope, GameplayDomainDescriptor descriptor)
        {
            _runner = runner;
            _launcherScope = launcherScope;
            _descriptor = descriptor;
        }

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
    public sealed partial class GameplayResult : OneOfBase<GameplayResult.Won, GameplayResult.Lost, GameplayResult.QuitToMenu>
    {
        public readonly record struct Won(int Score, TimeSpan Time);
        public readonly record struct Lost(int Score);
        public readonly record struct QuitToMenu;
    }
}
```

Rules:

- The signature is always `UniTask<TResult> RunAsync(TArgs args, Transition transition, CancellationToken ct)`. `TArgs` is a `sealed record` (use an empty record if there are no args). `TResult` is a **named union** (§7.2). A domain that never ends by itself (long-lived, for example a HUD running alongside Gameplay) still declares a result type. It ends only through cancellation, and its union typically has a single case.
- `CreateDebug()` MUST exist on every main and leaf domain's args, under `#if UNITY_EDITOR`. Sub-domains cannot be debug-run (§4.8): their entry class does not implement `IDebugRunnableDomain` (no `Descriptor`, no `RunDebugAsync`) and their args have no `CreateDebug()`.
- The entry class is registered in the **launcher's** scope (§4.8, §5.3) and injects the launcher's `ScopeRef`. That is how the runner knows the parent.

### 4.4 Domain descriptor and content root

```csharp
namespace Core.Domains
{
    public abstract class DomainDescriptor : ScriptableObject
    {
        [field: SerializeField, Required] public string ContentDirectoryName { get; private set; } = null!;
        [field: SerializeField] public LogTag LogTag { get; private set; }

#if UNITY_EDITOR
        [field: SerializeField, Required] public DomainContent EditorContent { get; private set; } = null!;
#endif
    }

    public abstract class DomainContent : ScriptableObject
    {
        [field: SerializeField] public LoadableSceneId ScopeScene { get; private set; }
    }
}

namespace Gameplay
{
    [CreateAssetMenu(menuName = "Domains/Gameplay Descriptor")]
    public sealed class GameplayDomainDescriptor : DomainDescriptor
    {
    }

    [CreateAssetMenu(menuName = "Domains/Gameplay Content")]
    internal sealed class GameplayContent : DomainContent
    {
        [field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];
    }
}
```

The **descriptor** is referenced from outside the domain (root prefab, launcher scopes), so it ends up in the player build. Player-build assets MUST NOT contain `Loadable<T>` or `LoadableSceneId` (§10.1, verified in §15.1). The descriptor therefore holds only the content directory name and the log tag. The **content root** (`<Name>Content.asset`, a `DomainContent` subclass) is the root asset of the domain's content directory. It holds the scope scene, content scenes, and `Loadable<T>` references. Configs registered in the domain scope usually live on the `<Name>LifetimeScope` component instead (§5.3).

`EditorContent` is an editor-only reference to the content root. It does not pull the content into the player build (verified in §15.1). The Editor uses it to load content without building directories (§10.1), and the play-from-any-scene hook (§4.8) uses it to find the scope scene (`LoadableSceneIdEditorUtility.LoadableSceneIdToScene(EditorContent.ScopeScene)`). `LoadableSceneId` fields are authored in the inspector by dragging a scene asset (Unity ships an IMGUI + UI Toolkit drawer, so Odin inspectors show it too), or in code with `LoadableSceneIdEditorUtility.CreateLoadableSceneId(path)`.

### 4.5 `DomainRunner` (Core)

Responsibilities, in order:

1. **Guards.** Parent depth ≤ 1 (else throws). One running instance per descriptor type (a second run is a bug, so it throws). Both throw from the returned `UniTask` before anything else happens.
2. `Transition.Loading`: `await loadingScreen.ShowAsync(ct)` (§9.5). It returns at once when the screen is already up, for example left up by the previous domain.
3. Take the **load gate** (a `SemaphoreSlim(1)`). `LifetimeScope.EnqueueParent` / `Enqueue` are static and process-wide, so two domains loading concurrently would race. The gate serializes *only the scene-load + scope-build window*. Domains still *run* in parallel.
4. Resolve the content root: `IContentDirectoryRegistry.GetContent(descriptor)` (§10.1). `NotFound` is a configuration bug, so it throws. Inside `using (LifetimeScope.EnqueueParent(parent.Scope))` and `using (LifetimeScope.Enqueue(builder => ...))`:
   - load `content.ScopeScene` additively through `ISceneLoader` with `CancellationToken.None` (a scene load cannot be aborted; a missing scope scene throws),
   - the extra installer registers: `args` (as `TArgs`), the content root instance as its concrete type (for example `GameplayContent`), a new `DomainCompletion<TResult>`, the per-domain `LocalizedLabelBinder` entry point (§10.5), and a build callback that marks the build as completed.
5. Release the gate. Find the built scope with `LifetimeScope.Find<DomainLifetimeScope>(scene)`. A scope scene without a `DomainLifetimeScope`, or a scope whose build did not complete, throws `InvalidOperationException`. Then `ct.ThrowIfCancellationRequested()`.
6. `Transition.Loading`: wait until the domain is **ready**, then `await loadingScreen.HideAsync(ct)`. Ready means: one frame has passed (`UniTask.Yield(Update)`, so VContainer has started the scope's entry points in its `Startup` phase) and every content scene they started loading through `DomainSceneSet` has loaded. Gameplay's room is therefore loaded before the screen fades out.
7. `await completion.Task` (with `ct` attached).
8. `Transition.Loading`: `await loadingScreen.ShowAsync(ct)` **before** the teardown, so the end of the domain is covered. Then return the result.
9. `finally` (always, also on cancellation and failure), with `CancellationToken.None`:
   - dispose the scope (this disposes every `IDisposable` registered in it and cancels `IAsyncStartable` tokens),
   - unload the content scenes the domain loaded through its `DomainSceneSet` (§4.6, reverse load order, after awaiting in-flight loads), then the scope scene,
   - release the "running" guard (even if teardown throws).

   Teardown MUST complete.
10. **Handoff.** A `Transition.Loading` run that **returns** a result leaves the loading screen **up**: the launcher's next `Transition.Loading` run finds it up in step 2 and takes it down in step 6, so neither the teardown of one domain nor the loading of the next is ever visible. A run that **throws** (guard failure, missing content, entry-point failure, cancellation) takes the screen down after its teardown, so a failed or cancelled switch never leaves the game behind a loading screen. A launcher that returns from a `Transition.Loading` run and then does something other than start the next one hides the screen itself; `GameFlow` does not, because it only quits.

`Transition.None` runs never touch the loading screen. Overlays (Settings, Pause) and parallel domains (§4.7) use `None`, so they never show it (D35). Only one flow at a time uses `Transition.Loading` (the game flow's main domains).

```csharp
namespace Core.Domains
{
    public sealed class DomainRunner : IDisposable
    {
        public DomainRunner(ILoadingScreen loadingScreen, IContentDirectoryRegistry contentDirectories, ISceneLoader sceneLoader);

        public UniTask<TResult> RunAsync<TArgs, TResult>(DomainDescriptor descriptor, ScopeRef parent, TArgs args, Transition transition, CancellationToken ct)
            where TArgs : class
            where TResult : class;

        public void Dispose();
    }

    public sealed class DomainCompletion<TResult> : IDomainCompletion where TResult : class
    {
        public UniTask<TResult> Task { get; }
        public bool IsCompleted { get; }

        public void Complete(TResult result);
    }

    internal interface IDomainCompletion
    {
        void Fail(Exception exception);
    }

    public sealed record ScopeRef(LifetimeScope Scope, int Depth);

    public enum Transition
    {
        None = 0,
        Loading = 1,
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

`ScopeRef` registration: `RootLifetimeScope` registers `new ScopeRef(this, 0)`. `DomainLifetimeScope.Configure` registers `new ScopeRef(this, parentDepth + 1)`, where `parentDepth` is resolved from the parent container. Because child registrations shadow the parent's, and domain entry classes are registered `Lifetime.Scoped` (§4.8), anything resolved inside a scope gets **that** scope's `ScopeRef`.

Ending a domain: any presenter or service inside the domain injects `DomainCompletion<TResult>` and calls `Complete(...)`. Usually exactly one "flow" presenter owns the completion. Scattering `Complete` calls is discouraged. `Complete(null)` throws `ArgumentNullException`; a second `Complete` throws `InvalidOperationException` (guard with `IsCompleted` where two inputs may race, such as two buttons).

**Failure.** Every exception thrown by an entry point of a domain scope (`IInitializable`, `IStartable`, `IAsyncStartable`, ticks) goes to the domain's entry-point exception handler, which `DomainLifetimeScope.Configure` registers. `OperationCanceledException` is ignored (it is how a disposed scope stops its entry points). Any other exception is logged with `Log.Exception` and fails the run through `IDomainCompletion.Fail`: `RunAsync` throws that exception to the launcher after the normal teardown. The first outcome wins: `Fail` after `Complete` is ignored. The root scope uses the same handler without the fail step.

### 4.6 Content scenes inside a domain

Only the domain's own code loads and unloads content scenes, through `DomainSceneSet`. It is a scoped Core service, registered automatically in every domain scope, that loads `LoadableSceneId`s additively and emits every loaded scene on `SceneLoaded`. The runner unloads everything it loaded when the domain ends (§4.5 step 8).

```csharp
namespace Core.Domains
{
    public sealed class DomainSceneSet : IDisposable
    {
        public Observable<Scene> SceneLoaded { get; }

        public UniTask<OneOf<Scene, NotFound>> LoadAsync(LoadableSceneId id, CancellationToken ct);
        public UniTask UnloadAsync(Scene scene, CancellationToken ct);
        public void Dispose();
    }
}
```

`UnloadAsync` of a scene this set did not load throws. Content scenes contain no `LifetimeScope` and no logic. Their MonoBehaviours are views or plain scene objects. When presenters need views from a content scene, the domain's flow presenter locates them after load by a known root component (for example `RoomView` on a root object) and passes them on. Views in content scenes are not registered through the scope's serialized fields.

### 4.7 Parallel domains

- A launcher MAY run several domains concurrently, for example `await UniTask.WhenAny(gameplay.RunAsync(...), hud.RunAsync(...))`.
- Long-lived domains end through cancellation. The launcher creates a linked `CancellationTokenSource`, cancels it, and the runner's `finally` tears the domain down. The resulting `OperationCanceledException` propagates to the launcher, which handles it deliberately (§7.5).
- Only one instance per domain type runs at a time.
- Example: `GameFlow` starts the `Loading` domain at boot, next to the main domains, and never awaits it until the game quits: `var loadingRun = _loading.RunAsync(new LoadingArgs(), Transition.None, ct);`. It lives as long as the flow, so it ends through cancellation of the flow's own token when the root scope is disposed (the resulting `OperationCanceledException` ends `StartAsync`, which the root's entry-point handler ignores). A linked `CancellationTokenSource` is needed only to end a long-lived domain before its launcher.
- A parallel domain runs with `Transition.None`, so parallel domains never show the loading screen (D35).

### 4.8 Boot, the game flow, and play-from-any-scene

**Root scope.** `RootLifetimeScope` (Bootstrap) is the `VContainerSettings.RootLifetimeScope` prefab. VContainer instantiates it automatically before the first scene, and nothing ever destroys it. It contains the Main `Camera` with `CinemachineBrain` and `AudioListener`, the `EventSystem` with `InputSystemUIInputModule`, and `AudioSources` (the `AudioSourceSet`, §9.4). The loading screen is not on the root prefab: the `Loading` domain authors it in its scope scene (§9.5). Its `Configure`:

```csharp
namespace Bootstrap
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField, Required] private CoreConfig _coreConfig = null!;
        [SerializeField, Required] private AudioSourceSet _audioSources = null!;
        [SerializeField, Required] private MainMenuDomainDescriptor _mainMenuDescriptor = null!;
        [SerializeField, Required] private GameplayDomainDescriptor _gameplayDescriptor = null!;
        [SerializeField, Required] private SettingsDomainDescriptor _settingsDescriptor = null!;
        [SerializeField, Required] private LoadingDomainDescriptor _loadingDescriptor = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new ScopeRef(this, 0));
            CoreInstaller.Install(builder, _coreConfig, _audioSources, GetStorageRoot());
            builder.Register<LoadingScreen>(Lifetime.Singleton).AsSelf().As<ILoadingScreen>();
            builder.RegisterDomain<MainMenuDomain>(_mainMenuDescriptor);
            builder.RegisterDomain<GameplayDomain>(_gameplayDescriptor);
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
            builder.RegisterDomain<LoadingDomain>(_loadingDescriptor);

            switch (BootMode.Current)
            {
                case BootMode.Kind.Normal:
                    builder.RegisterEntryPoint<GameFlow>();
                    break;
#if UNITY_EDITOR
                case BootMode.Kind.DebugDomain:
                    builder.RegisterEntryPoint<DebugDomainBoot>();
                    break;
                case BootMode.Kind.Test:
                    break;
#endif
                default:
                    throw new InvalidOperationException($"Unsupported boot mode {BootMode.Current}.");
            }
        }

        private static string GetStorageRoot()
        {
#if UNITY_EDITOR
            if (BootMode.Current == BootMode.Kind.Test)
            {
                return BootMode.TestStorageRoot;
            }
#endif
            return CoreInstaller.DefaultStorageRoot;
        }
    }
}
```

`RegisterDomain<TDomain>(descriptor)` (Core, `TDomain : class, IDebugRunnableDomain`) registers the descriptor instance as its concrete type and `TDomain` as `Lifetime.Scoped` `.AsSelf().As<IDebugRunnableDomain>()`. Scoped means the entry class is built in the resolving scope, so it always receives that scope's `ScopeRef` and the depth guard sees the real parent. `RegisterSubDomain<TDomain>(descriptor)` (`TDomain : class`) does the same without `.As<IDebugRunnableDomain>()`, for sub-domain entry classes. Every scope that launches a domain registers it itself (the root registers its main domains and the leaves it may debug-run; `MainMenuLifetimeScope` and `GameplayLifetimeScope` register `SettingsDomain`; `GameplayLifetimeScope` registers `PauseDomain` with `RegisterSubDomain`).

**Boot modes.** `BootMode` (Bootstrap) is a static class with `enum Kind { None = 0, Normal = 1, DebugDomain = 2, Test = 3 }`. `BootMode.Current` is resolved at `RuntimeInitializeLoadType.SubsystemRegistration`, before the root builds:

| Kind | When | Root registers |
|---|---|---|
| `Normal` | always in players; in the Editor when no SessionState key is set | `GameFlow` |
| `DebugDomain` (Editor) | SessionState `BootMode.DebugScopeScenePathKey` holds a scope-scene path | `DebugDomainBoot` |
| `Test` (Editor) | SessionState bool `BootMode.TestKey` is set (wins over `DebugDomain`) | no flow entry point; the test is the flow (§14.2) and the storage root is `BootMode.TestStorageRoot` |

**Startup ownership.** The flow entry point owns Core startup: `GameFlow`, `DebugDomainBoot` and the smoke test each `await CoreStartup.RunAsync(ct)` first (§8). Entry points start in parallel, so a separate Core entry point could not guarantee "loaded before the first domain".

**GameFlow** (Bootstrap): an `IAsyncStartable` that reads like a script. It is the only place that knows the order of the main domains.

```csharp
public async UniTask StartAsync(CancellationToken ct)
{
    await _coreStartup.RunAsync(ct);

    var loadingRun = _loading.RunAsync(new LoadingArgs(), Transition.None, ct);
    await RunMenuAndGameplayAsync(ct);

    Log.Info(LogTags.Flow, "Quitting.");
    _application.Quit();
    await loadingRun;
}

private async UniTask RunMenuAndGameplayAsync(CancellationToken ct)
{
    while (true)
    {
        var menuResult = await _mainMenu.RunAsync(new MainMenuArgs(), Transition.Loading, ct);
        var shouldQuit = menuResult.Match(
            play => false,
            quit => true);

        if (shouldQuit)
        {
            return;
        }

        var gameplayResult = await _gameplay.RunAsync(new GameplayArgs(LevelIndex: 0), Transition.Loading, ct);
        gameplayResult.Switch(
            won => Log.Info(LogTags.Flow, $"Won with {won.Score} points in {won.Time.TotalSeconds:0.0} s."),
            lost => Log.Info(LogTags.Flow, $"Lost with {lost.Score} points."),
            quitToMenu => Log.Info(LogTags.Flow, "Quit to menu."));
    }
}
```

The `Loading` domain runs for the whole session. The flow switches its main domains with `Transition.Loading`, so the screen covers every switch (§4.5 step 10). After the player quits, the flow keeps awaiting the Loading run, so the screen stays up while the application shuts down.

**Build settings.** Only `Bootstrap.unity` is in Build Settings. It is empty. Domain scenes are loaded from content directories and need no Build Settings entry (verified in §15.1, Editor and standalone player).

**Play from any scene (Editor only).**

1. `Bootstrap.Editor.PlayFromAnySceneHook` (`[InitializeOnLoad]`, `EditorApplication.playModeStateChanged`) runs on `ExitingEditMode`. The **root-registered** descriptors are the `DomainDescriptor` references serialized on the `RootLifetimeScope` component of the `VContainerSettings` root prefab.
   - If the active scene, or else the first other loaded scene (additive editing), is the scope scene of a root-registered descriptor, the hook stores its path in `BootMode.DebugScopeScenePathKey` and sets `EditorSceneManager.playModeStartScene` to `Bootstrap.unity`.
   - Otherwise, if a loaded scene is the scope scene of any other `DomainDescriptor` (a sub-domain or a leaf the root does not register), the hook logs a warning and starts from `Bootstrap.unity` in `Normal`, because a scope scene MUST never play without the runner (§5.3).
   - Otherwise nothing is redirected: the open scenes play and the root boots `Normal` next to them.
2. On `EnteredEditMode` the hook clears `playModeStartScene`, the debug key and `BootMode.TestKey` (so an aborted test run cannot leave Play mode booting into `Test`).
3. `DebugDomainBoot` (Editor-only entry point) does not start the `Loading` domain: debug runs use `Transition.None` (`RunDebugAsync`), so nothing needs the screen. Debug-running the `Loading` scope scene itself runs the domain with its screen hidden. `DebugDomainBoot` awaits `CoreStartup.RunAsync`, finds the `IDebugRunnableDomain` whose `Descriptor.EditorContent` scope scene matches the stored path (`EditorScopeScene.IsScopeSceneOf`; no match throws), awaits `RunDebugAsync(ct)`, logs the result, and sets `EditorApplication.isPlaying = false`.
4. The Editor restores the originally open scenes after Play mode (Unity's default behaviour with `playModeStartScene`).

---

## 5. Dependency injection and lifetime (VContainer)

### 5.1 Scope tree

```
Root (RootLifetimeScope prefab, depth 0) ── Core services, LoadingScreen, domain entry classes, flow entry point
 ├─ Loading scope (depth 1, runs alongside the others for the whole session)
 ├─ MainMenu scope (depth 1)
 │   └─ Settings scope (leaf, depth 2)
 └─ Gameplay scope (depth 1)
     ├─ Pause scope (sub, depth 2)
     │   (Pause cannot launch Settings: depth limit. Pause returns OpenSettings and Gameplay launches Settings, see §13.)
     └─ Settings scope (leaf, depth 2)
```

### 5.2 Registration rules

- **Constructor injection only**, into plain C# classes. `[Inject]` on fields, properties, or methods is **forbidden** everywhere. MonoBehaviours are **never** injected into.
- MonoBehaviours enter the container as **instances** that are authored in the scene or the root prefab. Prefer `[SerializeField]` references on the scope component + `builder.RegisterComponent(_view)`. `RegisterComponentInHierarchy<T>()` is allowed for singletons in the scope scene. `RegisterComponentOnNewGameObject` and `RegisterComponentInNewPrefab` are **forbidden** (`Docs/Rules.md` §1).
- Presenters and anything that needs a lifecycle: `builder.RegisterEntryPoint<T>()`. Use `.AsSelf()` if something else needs to resolve it (for example `GameplayFlowPresenter` calling `PlayerMovementPresenter.PlaceAt`).
- Services: `builder.Register<IFoo, Foo>(Lifetime.Singleton)` (a singleton *per scope*). Use an interface when there is a real seam (tests, multiple implementations, or Core contracts implemented by domains). Otherwise register the concrete type. `Lifetime.Transient` needs a reason.
- Configs (ScriptableObjects): `builder.RegisterInstance(_config)` from a serialized field on the scope.
- Scene objects that are not views (the audio sources, the storage root path) are passed to `CoreInstaller.Install` as parameters and wrapped by registration lambdas, so the container never holds loose scene objects.
- `IObjectResolver` MAY only be used in registration lambdas and build callbacks (`CoreInstaller`, `DomainRunner`, `DomainLifetimeScope`, `RegisterLocalizationTable`). Everyone else gets their dependencies explicitly.
- `LifetimeScope.Find`, `FindObjectOfType`, `GameObject.Find`, and singletons (`static Instance`) are **forbidden** in game code. Exceptions: `DomainRunner` finding the freshly built scope, and the flow presenter locating content-scene roots (§4.6).
- Optional cross-domain contracts (escape hatch from §4, use rarely): Core declares `IFoo`. Bootstrap registers the real implementation (from a domain) in the root, **or** registers a Core-provided `NullFoo` when that domain is deleted. Consumers never check for presence. The implementation class is public in its domain's assembly (the one exception to §4.1's visibility rule). Example: `ILoadingScreen`, implemented by `Loading.LoadingScreen`, with `NullLoadingScreen` as the Core default (§9.5).
- High managed-code stripping can break reflection-based injection (VContainer issue #863). Keep Managed Stripping Level at its current setting, or add `link.xml` entries for first-party assemblies. The VContainer source generator MAY be added later. Not now.

### 5.3 `DomainLifetimeScope`

```csharp
namespace Core.Domains
{
    public abstract class DomainLifetimeScope : LifetimeScope
    {
        protected sealed override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                throw CreateNotBuiltByRunnerException();
            }

            var parentDepth = Parent.Container.Resolve<ScopeRef>().Depth;
            builder.RegisterBuildCallback(EnsureBuiltByRunner);
            builder.RegisterDomainEntryPointFailureHandler();
            builder.RegisterInstance(new ScopeRef(this, parentDepth + 1));
            builder.Register<DomainSceneSet>(Lifetime.Singleton);
            ConfigureDomain(builder);
        }

        protected abstract void ConfigureDomain(IContainerBuilder builder);

        private void EnsureBuiltByRunner(IObjectResolver resolver)
        {
            if (!resolver.TryResolve<IDomainCompletion>(out _))
            {
                throw CreateNotBuiltByRunnerException();
            }
        }

        private InvalidOperationException CreateNotBuiltByRunnerException()
        {
            return new InvalidOperationException($"{GetType().Name} was not built by {nameof(DomainRunner)}.");
        }
    }
}

namespace Gameplay
{
    internal sealed class GameplayLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private LocalizationTable _text = null!;
        [SerializeField, Required] private GameplayConfig _config = null!;
        [SerializeField, Required] private PlayerView _playerView = null!;
        [SerializeField, Required] private GameplayCameraView _cameraView = null!;
        [SerializeField, Required] private HudView _hudView = null!;
        [SerializeField, Required] private RoundResultView _resultView = null!;
        [SerializeField, Required] private PauseDomainDescriptor _pauseDescriptor = null!;
        [SerializeField, Required] private SettingsDomainDescriptor _settingsDescriptor = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterInstance(_config);
            builder.RegisterComponent(_playerView);
            builder.RegisterComponent(_cameraView);
            builder.RegisterComponent(_hudView);
            builder.RegisterComponent(_resultView);
            builder.Register<ScoreModel>(Lifetime.Singleton);
            builder.Register<RoundService>(Lifetime.Singleton);
            builder.Register<GameplaySettingsService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameplayFlowPresenter>();
            builder.RegisterEntryPoint<HudPresenter>();
            builder.RegisterEntryPoint<PlayerMovementPresenter>().AsSelf();
            builder.RegisterSubDomain<PauseDomain>(_pauseDescriptor);
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
        }
    }
}
```

- The failure handler MUST be registered in `Configure` before `ConfigureDomain`: VContainer adds its own default handler on the first `RegisterEntryPoint` when the builder has none, and two handlers are ambiguous.
- `EnsureBuiltByRunner` runs before any entry point starts, so a scope scene opened without the runner (for example loaded additively by hand) fails with a clear message instead of running half-wired.
- Scope scene `LifetimeScope` inspector settings: `autoRun = true`, parent reference **empty** (the runner supplies it through `EnqueueParent`). The play-from-any-scene hook (§4.8) guarantees that a scope scene never plays without the runner.
- VContainer's script template processor overwrites any **newly created** `*LifetimeScope.cs` file with an empty template when Unity imports it. Write the file's `.meta` (fresh GUID) before the first import, or rewrite the file after import and verify it.

### 5.4 Entry points and lifecycle interfaces

| Interface (VContainer) | Use for |
|---|---|
| `IInitializable` | synchronous setup that must happen before `Start` (rare; e.g. registering a localization table) |
| `IStartable` | subscribe to views, input callbacks, lock service, and state |
| `IAsyncStartable` | async flows. Its `StartAsync(CancellationToken)` token is cancelled when the scope is disposed. **This is the domain's primary lifetime token.** |
| `ITickable` / `IFixedTickable` / `ILateTickable` | **only** to apply continuous values every frame (movement, camera, audio fades), or to drive `TimerService`. Not for polling input and not for flow. |
| `IDisposable` | release subscriptions, remove input callbacks, unsubscribe from locks, dispose `DisposableBag` |

### 5.5 Async and cancellation rules (UniTask)

- Every async method returns `UniTask` / `UniTask<T>` (never `Task`, except test methods, §14), and takes `CancellationToken ct` as its **last parameter, without a default value**. This includes private helpers. A caller that must not be cancelled passes `CancellationToken.None` explicitly.
- The token comes from the caller. The root of every chain is either `IAsyncStartable.StartAsync`'s token (scope lifetime), the token `SubscribeAwait` passes (§6.3), or a linked source owned by the code that may cancel early.
- `async void` is **forbidden**. Fire-and-forget: an `async UniTaskVoid` method + `.Forget()`. The method takes a token and handles its own expected failures. This is allowed only where nothing awaits the outcome (for example `SaveAutoFlush` flushing on focus loss).
- MonoBehaviour async code (views only, for visuals) uses `destroyCancellationToken`, linked with the caller's token when one is given.
- Waiting: `UniTask.Delay(..., cancellationToken: ct)`, `UniTask.Yield(PlayerLoopTiming.Update, ct)`. **Game timers that must stay aligned use `TimerService`** (§9.3), not ad-hoc delays.
- Do not use `.GetAwaiter().GetResult()`, `.Result`, `Task.Run`, or threads for game logic.

---

## 6. Presentation (MVP)

### 6.1 Roles

| Role | Type | Knows | Never |
|---|---|---|---|
| **View** | `MonoBehaviour`, suffix `View` | its serialized children (buttons, labels, transforms, animators, particle systems) | presenters, services, models, DI, game rules |
| **Presenter** | plain C# class, suffix `Presenter` (`InputHandler` for input translators, §9.1) | its views (concrete types), services, models, `DomainCompletion` | `GetComponent`, `Find`, Unity lifecycle methods (except the flow presenter locating content-scene roots, §4.6) |
| **Model / State** | plain C# class, suffix `Model` (mutable state holder) or `State` (immutable snapshot record) | nothing about Unity presentation | views |
| **Service** | plain C# class, suffix `Service` (Core and domain-level) | models, other services, Core adapters | views |

Presenters depend on **concrete view classes** (no `IFooView` interfaces by default). Add an interface only when a presenter is complex enough to deserve unit tests (§14.2), as the Loading domain does with `ILoadingScreenView`.

### 6.2 View rules

- **Outputs** (user intent) are `Observable<T>` properties built from UI components or R3 triggers:
  ```csharp
  public Observable<Unit> PlayClicked => _playButton.OnClickAsObservable();
  public Observable<Unit> Touched => this.OnTriggerEnterAsObservable().AsUnitObservable();
  ```
- **Inputs** are imperative methods starting with a verb: `SetScore(int score)`, `Show(...)`, `PlayAsync(CancellationToken ct)`, `WaitForContinueAsync(CancellationToken ct)`.
- A view MAY contain **purely visual** code: animations, particle playback, layout, hand-written UniTask fades. It MUST NOT decide game outcomes.
- A view MAY read its own serialized visual config (colors, durations, offsets).
- A view shows and hides its authored children with `SetActive` / `enabled`. It never instantiates or destroys objects (§6.6).
- One MonoBehaviour per file. The file name matches the class name.
- Unity messages allowed in views: `Awake` (caching visual components or wiring purely visual listeners), `OnValidate`, `OnDrawGizmos`. `Update` / `FixedUpdate` in views are **discouraged**. Movement and continuous logic are driven by presenters through `ITickable` calling view methods. Physics callbacks reach presenters as observables (`OnTriggerEnterAsObservable`), not as Unity messages.

```csharp
namespace MainMenu
{
    internal sealed class MainMenuView : MonoBehaviour
    {
        public Observable<Unit> PlayClicked => _playButton.OnClickAsObservable();
        public Observable<Unit> SettingsClicked => _settingsButton.OnClickAsObservable();
        public Observable<Unit> QuitClicked => _quitButton.OnClickAsObservable();

        [SerializeField, Required] private Button _playButton = null!;
        [SerializeField, Required] private Button _settingsButton = null!;
        [SerializeField, Required] private Button _quitButton = null!;
        [SerializeField, Required] private TMP_Text _versionLabel = null!;

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
        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;

        private readonly MainMenuView _view;
        private readonly IApplicationService _application;
        private readonly ILocalizationService _localization;
        private readonly IInputService _input;
        private readonly SettingsDomain _settingsDomain;
        private readonly DomainCompletion<MainMenuResult> _completion;

        public MainMenuPresenter(MainMenuView view, IApplicationService application, ILocalizationService localization, IInputService input, SettingsDomain settingsDomain, DomainCompletion<MainMenuResult> completion)
        {
            _view = view;
            _application = application;
            _localization = localization;
            _input = input;
            _settingsDomain = settingsDomain;
            _completion = completion;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);

            _localization.Current.Subscribe(_ => _view.SetVersion(_localization.Format(SharedText.Version, _application.Version))).AddTo(ref _subscriptions);
            _view.PlayClicked.Subscribe(_ => Complete(new MainMenuResult.Play())).AddTo(ref _subscriptions);
            _view.QuitClicked.Subscribe(_ => Complete(new MainMenuResult.Quit())).AddTo(ref _subscriptions);
            _view.SettingsClicked
                .SubscribeAwait((_, ct) => OpenSettingsAsync(ct).AsValueTask(), AwaitOperation.Drop)
                .AddTo(ref _subscriptions);
        }

        private void Complete(MainMenuResult result)
        {
            if (_completion.IsCompleted)
            {
                return;
            }

            _view.SetInteractable(false);
            _completion.Complete(result);
        }

        private async UniTask OpenSettingsAsync(CancellationToken ct)
        {
            _view.SetInteractable(false);

            try
            {
                await _settingsDomain.RunAsync(new SettingsArgs(), Transition.None, ct);
            }
            finally
            {
                _view.SetInteractable(true);
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
```

**Async handlers on view outputs (the one pattern, verified in §15.7):** `SubscribeAwait((_, ct) => XAsync(ct).AsValueTask(), AwaitOperation.Drop)`, where `XAsync` is a normal `async UniTask` method (§5.5). `AsValueTask()` is UniTask's extension. R3 cancels the `ct` it passes when the subscription is disposed, so no extra `CancellationTokenSource` is needed; the cancellation is swallowed by R3 and logs nothing. `AwaitOperation.Drop` ignores emissions while the handler runs. Do not use `async UniTaskVoid` handlers with busy flags.

- Presenters own a `DisposableBag` (R3 struct). Every subscription is added to it with `.AddTo(ref _subscriptions)`. `Dispose()` disposes it.
- Presenters that start async work own their lifetime through `IAsyncStartable`'s token, through the token `SubscribeAwait` passes, or through a private `CancellationTokenSource` cancelled in `Dispose()`.
- One presenter per view by default. One presenter MAY drive several related views. A view MUST NOT be driven by two presenters. Another presenter that needs the view goes through the owning presenter (`GameplayCameraPresenter.Follow(target)`, `PlayerMovementPresenter.PlaceAt(position)`).

### 6.4 Models and observable state

- State that one presenter reads is a plain field or property.
- State that several parties observe is a `ReactiveProperty<T>`, kept **private**, and exposed as `ReadOnlyReactiveProperty<T>`:
  ```csharp
  internal sealed class ScoreModel : IDisposable
  {
      public ReadOnlyReactiveProperty<int> Score => _score;

      private readonly ReactiveProperty<int> _score = new(0);

      public void Add(int points)
      {
          if (points <= 0)
          {
              throw new ArgumentOutOfRangeException(nameof(points), points, "Points must be positive.");
          }

          _score.Value += points;
      }

      public void Dispose()
      {
          _score.Dispose();
      }
  }
  ```
- One-off notifications: a private `Subject<T>` exposed as `Observable<T>` on a small class registered in the scope (`PauseRequests`, `ResumeRequests`: `Observable<Unit> Requested` + `void Request()`). Public `Subject`s are forbidden.
- **Plain C# `event`s are not used** in first-party code. Use R3 for anything subscribable, for one mechanism with uniform disposal.

### 6.5 R3 usage limits (P7)

- Allowed in presenters and services: `Subscribe`, `Where`, `Select`, `DistinctUntilChanged`, `ThrottleFirst`, `Debounce`, `CombineLatest` (for binding two or three properties to one view), `SubscribeAwait`, `Skip`, `Take`.
- A single chain SHOULD have at most **three operators** before `Subscribe`.
- **Forbidden:** using observables for control flow between domains or services, `SelectMany` flattening of async flows, multi-hop pipelines that route events through several services, and `Subject`s used as a message bus. Flow is `async`/`await`. Cross-domain communication is `RunAsync` results.
- Frame and time operators use R3's Unity providers. Game-aligned ticking comes from `TimerService`.

### 6.6 Authored objects (no runtime creation)

`Docs/Rules.md` §1 is binding: game code never instantiates prefabs, creates GameObjects or adds components at runtime. Core has no view factory and no view pool.

- Every object that can exist at runtime is authored in the scope scene, a content scene or the root prefab, and referenced through serialized fields. Prefabs exist only to be placed into scenes in the Editor.
- A variable number of things is a **fixed authored set**, sized for the worst case in the Editor: one `PickupEffectView` nested under each `CollectibleView`, 16 authored SFX `AudioSource`s (§9.4).
- Show and hide with `SetActive` / `enabled` (`CollectibleView.Hide()` disables its trigger and visual and leaves its effect running).
- When a fixed set runs out, reuse an element (for example steal the oldest-started voice). Never grow it.
- **One presenter drives a collection of item views** (`CollectiblesPresenter` iterates `RoomView.Collectibles`). Create per-item presenters only when an item has substantial logic of its own.
- UI widgets that instantiate objects at runtime are not used. `TMP_Dropdown` builds its option list, items and a blocker every time it opens, so a choice among a few values is an authored `Shared.UI.SelectorView` (previous/next buttons and a value label; the presenter cycles through the values, as `SettingsPresenter` does with `CoreConfig.SupportedLanguages`).
- Allowed exceptions: VContainer instantiating the authored root prefab from `VContainerSettings`, the generated `GameInput` constructor (an in-memory `InputActionAsset`, no GameObject), and Editor tooling and tests.

---

## 7. Errors, results, logging

### 7.1 Policy

| Situation | Mechanism |
|---|---|
| Expected failure (missing file, parse failure, not enough gold, content not found, validation failure) | Return a **union** (OneOf). |
| Bug or broken invariant (null where impossible, double `Complete`, depth > 2, misconfigured descriptor or asset) | **Throw** (`InvalidOperationException`, `ArgumentException`) and fail loudly. Do not catch. In a domain the failure ends the run (§4.5). |
| Cancellation | UniTask's `OperationCanceledException`, the idiomatic flow. Not converted to unions. |
| Third-party API that throws for expected conditions (File IO, Newtonsoft, Input System JSON, platform APIs) | Wrap in an **edge adapter** that catches the *specific* exceptions and returns unions (§7.6). |

`try/catch` appears **only** in edge adapters and in cancellation boundaries (§7.5). `try/finally` is used for teardown and for restoring state (`OpenSettingsAsync` above). `catch (Exception)` is forbidden outside edge adapters. Where an adapter uses it, it MUST rethrow `OperationCanceledException`.

### 7.2 Union style

- **Public** API (domain results, service return types): **named unions** via `OneOf.SourceGenerator`:
  ```csharp
  [GenerateOneOf]
  public sealed partial class LoadSaveResult : OneOfBase<SaveData, NotFound, Corrupted>
  {
  }
  ```
  Named unions MUST be declared inside a namespace (the generator emits into the containing namespace).
- **Private and local** plumbing, and Core service returns built only from shared types, MAY use inline `OneOf<A, B>` (`OneOf<Success, Error>`).
- Case types are `readonly record struct` (payload-less or small) or `sealed record` (richer payloads). Result-specific cases are **nested** in the union class (`GameplayResult.Won`). Reusable cases live in `Core.Results`.
- Consume with `Match` (returns a value) or `Switch` (side effects), or `TryPickT0(out var value, out var remainder)` for early return (then `Match`/`Switch` the remainder, which keeps exhaustiveness). **`AsT0`/`AsT1` without a prior `IsT0` check is forbidden** (it throws).
- Success without a value: `OneOf.Types.Success`. Never write `using OneOf.Types;` next to `Core.Results` (both define `NotFound` and `Error`); alias it: `using Success = OneOf.Types.Success;`.
- Adding a case to a union intentionally breaks every `Match`/`Switch`. That is the point. Fix all of them.

### 7.3 Shared result types (`Core.Results`)

```csharp
namespace Core.Results
{
    public readonly record struct NotFound;
    public readonly record struct Corrupted(string Reason);
    public readonly record struct Error(string Message);
}
```

One type per file. There is **no** global `Error` hierarchy or error-code enum. Each domain defines its own specific error cases. `Error(string)` is only a generic fallback for adapter and persistence failures.

### 7.4 Handling rule

An error is **logged where it is handled, never where it is created**. A method that returns `NotFound` does not log. The caller that decides "fall back to defaults" logs, if logging is warranted. A service that handles a failure itself (`SettingsService.Read` falling back to a section's default, `SaveStore` keeping a newer section) logs there.

### 7.5 Cancellation boundaries

- `DomainRunner`: `finally` teardown. It does not catch cancellation, which propagates to the launcher.
- The entry-point exception handlers ignore `OperationCanceledException` (§4.5).
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

### 7.6 Edge adapters and seams (Core)

| Adapter | Wraps | Returns |
|---|---|---|
| `IFileStorage` / `FileStorage(string rootDirectory)` | `System.IO` below an absolute root; all paths are relative (an absolute or empty path throws). Read, atomic write (temp file flushed to disk + `File.Replace`/`Move`, directories created), delete (missing = success), exists | `UniTask<OneOf<string, NotFound, Error>>`, `UniTask<OneOf<Success, Error>>`, `OneOf<Success, Error>`, `bool` |
| `IJsonSerializer` | Newtonsoft (shared settings: camelCase properties, dictionary keys unchanged, `TypeNameHandling.None`, ignore nulls, `DateTime` UTC, `DateParseHandling.None`, indented) | `string Serialize<T>(T)`, `OneOf<T, Corrupted> Deserialize<T>(string)` (`"null"`/empty = `Corrupted`) |
| `IContentLoader`, `ISceneLoader` | Content Directories (`Loadable<T>`, `LoadableSceneId`) | `OneOf<T, NotFound>`, `OneOf<Scene, NotFound>` (§10.1) |
| `InputBindingOverrides.Load` | `LoadBindingOverridesFromJson` (catches `ArgumentException`/`NullReferenceException`, then removes all overrides) | `OneOf<Success, Corrupted>` |

Seams over static Unity APIs, so the logic around them is unit-tested: `IContentLoadManager` (`ContentLoadManager`), `IGraphicsDevice` (`QualitySettings`, `Screen`), `IApplicationService` (`Application`), `IRealClock`/`IGameClock`.

Game code never touches `System.IO`, `JsonConvert`, `Loadable<T>.LoadAsync`, `SceneManager` load/unload, or `ContentLoadManager` directly.

### 7.7 Logging

```csharp
namespace Core.Logging
{
    [Serializable]
    public struct LogTag
    {
        public readonly string Name => _name ?? string.Empty;

        [SerializeField] private string _name;

        public LogTag(string name)
        {
            _name = name;
        }
    }

    public static class Log
    {
        [Conditional("GAME_LOG_VERBOSE")]
        public static void Verbose(LogTag tag, string message);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(LogTag tag, string message);

        public static void Warn(LogTag tag, string message);
        public static void Error(LogTag tag, string message);
        public static void Exception(Exception exception);
    }
}
```

- `Log` is static on purpose: logging is infrastructure and is not injected.
- Each assembly (including Editor assemblies) declares `internal static class LogTags { public static readonly LogTag Gameplay = new("Gameplay"); }`. Domain descriptors also carry a `LogTag`, which the runner uses for its "Starting."/"Stopped." lines.
- Output format: `[Tag] message`. `Log.Exception` logs the exception unchanged (entry-point failures, §4.5). Direct `Debug.Log*` calls are forbidden outside `Log`.
- `LogTag` is a plain `[Serializable] struct` with a `[SerializeField] private string _name`, for use in descriptors. A `readonly record struct` does **not** serialize in Unity 6.6 (verified in §15.6). Unity never serializes `readonly` (including `init`-only) backing fields, so records are for runtime values (results, args, DTOs), never for Unity-serialized fields.

---

## 8. Core service catalogue

Everything below is registered in the **root** scope by `CoreInstaller.Install` unless marked *per-domain*:

```csharp
public static void Install(IContainerBuilder builder, CoreConfig config, AudioSourceSet audioSources, string storageRoot);
```

- `config` is the `CoreConfig` ScriptableObject (`Bootstrap/Settings/CoreConfig.asset`): the `AudioMixer`, the default and supported languages, the Shared localization table, and the default volumes. It holds asset references only.
- `audioSources` is a scene instance on the root prefab, so it is a parameter, not a config field.
- `storageRoot` is the absolute root of `IFileStorage`. `CoreInstaller.DefaultStorageRoot` is `{persistentDataPath}` in players and `{persistentDataPath}/Editor` in the Editor (so an Editor session never writes files a player on the same machine would read). The `Test` boot mode passes `BootMode.TestStorageRoot` instead (§14.2).
- `Install` first registers the root entry-point exception handler (§4.5), then every service.

| Service | Interface / type | Section |
|---|---|---|
| Startup | `CoreStartup` (`UniTask RunAsync(ct)`: loads settings, then selects save slot 0; runs once, a second call throws) | §4.8 |
| Domain runner | `DomainRunner` | §4.5 |
| Domain scene set | `DomainSceneSet` *(per-domain, auto)* | §4.6 |
| Content | `IContentDirectoryRegistry`, `IContentLoader`, `ISceneLoader` | §10.1 |
| Storage adapters | `IFileStorage`, `IJsonSerializer` | §7.6 |
| Save | `ISaveStore`; `SaveAutoFlush` (entry point) | §10.3 |
| Settings | `ISettingsService`, `ISettingsLoader`, `IGraphicsDevice` | §10.4 |
| Input | `GameInput` (generated), `IInputService`, `ILockService<InputLockTag>` | §9.1, §9.2 |
| Time | `ITimeService`, `IRealClock`, `IGameClock`, `ITimerService` | §9.3 |
| Audio | `IAudioService` | §9.4 |
| Loading screen | `ILoadingScreen` (contract only; Bootstrap registers the `Loading` domain's `LoadingScreen`, or `NullLoadingScreen` without that domain) | §9.5 |
| Localization | `ILocalizationService`; the Shared table; `LocalizedLabelBinder` *(root, and per-domain auto)* | §10.5 |
| Application | `IApplicationService` (`Version`, `Platform`, `Quit()`; `Quit` leaves Play mode in the Editor) | §7.6 |
| Config | `CoreConfig` instance | — |

Not in Core: localization through Unity's package, networking, analytics, achievements, ads. Add these later as Core services or leaf domains through the escape hatch in §5.2.

---

## 9. Core runtime services

### 9.1 Input

- One asset: `Core/Input/GameInput.inputactions`, with **Generate C# Class** on: class `GameInput`, namespace `Core.Input`, output `Core/Code/Input/GameInput.cs` (committed; its generated comments are tool-owned). Input System settings have no project-wide actions, so the actions exist only once.
- Action maps: `Player` (Move, Look, Jump, Interact, Pause) and `UI` (Navigate, Submit, Cancel, Point, Click, ScrollWheel). Control schemes: Keyboard&Mouse, Gamepad, Joystick. Escape is both `Player/Pause` and `UI/Cancel`.
- `GameInput` is registered as a root singleton (`builder.Register<GameInput>(Lifetime.Singleton)`), so VContainer disposes it.
- **Reading input uses the generated callback interfaces** (`GameInput.IPlayerActions`), **not polling**:
  - One **input handler** per (domain, map), suffix `InputHandler`. It implements the generated interface, calls `AddCallbacks(this)` in `Start` and `RemoveCallbacks(this)` in `Dispose`, and translates raw input into domain intent: it writes to a model (`PlayerInputState.Move`) or raises a request (`_pauseRequests.Request()`).
  - Discrete actions act directly in the callback (on `context.performed`).
  - Continuous actions (Move, Look) cache the value in the callback (`performed` → value, `canceled` → zero). A separate `ITickable`/`IFixedTickable` presenter **applies** it each frame.
  - Generated interfaces require every action of the map to be implemented. Unused actions get an empty body. This is the reason for keeping handlers per map.
- `IInputService` owns **map activation** as a stack:
  ```csharp
  public interface IInputService
  {
      GameInput Actions { get; }

      IDisposable Push(InputMaps maps);
  }

  [Flags]
  public enum InputMaps
  {
      None = 0,
      Player = 1 << 0,
      Ui = 1 << 1,
  }
  ```
  `Push` enables exactly `maps`. Disposing the handle removes it wherever it sits in the stack and re-applies the top; an empty stack disables every map; double dispose is a no-op. A domain pushes its maps in its flow presenter's `Start` (or `StartAsync`) and disposes the handle in `Dispose`. Nested domains (Pause, Settings) push `Ui` on top of Gameplay's `Player | Ui`.
- The `InputSystemUIInputModule` on the root prefab keeps its own default actions. The `UI` map is for input handlers (for example Cancel = resume); pushing or popping it does not gate uGUI pointer and navigation.
- Rebinding: `GameInput.asset.SaveBindingOverridesAsJson()` produces the JSON; `ISettingsService` persists it and applies it through `InputBindingOverrides.Load` (§10.4).

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
- Root registration: `builder.RegisterInstance(new BaseLockService<InputLockTag>().WithDebug())` (typed `ILockService<InputLockTag>`). `WithDebug()` (`Migs.MLock.Debugging`) registers the service with MLock's debug windows (`Window/MLock/Locks Debug`, `Window/MLock/Services Debug`). Without it the windows stay empty. The registration is `[Conditional("UNITY_EDITOR")]`, so it costs nothing in players. Domain-local lock services (below) also call `WithDebug()` on creation and `WithoutDebug()` when their scope is disposed, because MLock keeps registered services in a static set.
- Input handlers implement `ILockable<InputLockTag>`:
  - `LockTags` returns the tags they belong to (`PlayerInputHandler`: `Movement | Pause`; `PauseInputHandler`: `Ui`).
  - They `Subscribe(this)` in `Start` and `Unsubscribe(this)` in `Dispose`.
  - `HandleLocking()` sets `_isLocked = true` and zeroes cached continuous values. `HandleUnlocking()` clears the flag and re-reads continuous values whose action is still enabled.
  - Callbacks return early while locked.
- Lockers (cutscenes, dialogs, transitions, pause, round end):
  ```csharp
  using var inputLock = _locks.Lock(InputLockTag.Movement | InputLockTag.Camera);
  await PlayCutsceneAsync(ct);
  ```
  Use `LockAll()` / `LockAllExcept(...)` as needed. A lock MUST be disposed on every path (`using`).
- Domains MAY create their own `BaseLockService<TheirTag>` for domain-local features, registered in their scope.
- The loading screen (`Loading.LoadingScreen`) holds `LockAll()` from `ShowAsync` until its fade-out has finished (§9.5).

### 9.3 Time

```csharp
public interface ITimeService
{
    ReadOnlyReactiveProperty<bool> IsPaused { get; }
    float TimeScale { get; set; }

    IDisposable Pause();
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IRealClock : IClock
{
}

public interface IGameClock : IClock
{
}

public interface ITimerService
{
    ITickSource Real { get; }
    ITickSource Game { get; }
}

public interface ITickSource
{
    Observable<Unit> EverySecond { get; }
    Observable<Unit> EveryMinute { get; }

    ReadOnlyReactiveProperty<TimeSpan> CountdownTo(DateTime utcEnd);
}
```

- `ITimeService.Pause()` is ref-counted: time is paused while any handle is alive (double dispose is a no-op). The service sets `UnityEngine.Time.timeScale` to 0 while paused, else to `TimeScale` (negative throws). Audio is not paused.
- `IRealClock` is wall-clock UTC: unscaled, runs during pause, and is the seam for future server time. `IGameClock` starts at boot and advances by scaled time (`Time.timeAsDouble`), so it stops while paused and is constant within a frame.
- One `ITickable` inside `TimerService` checks each clock for whole-second and whole-minute boundaries and emits then (once per tick, however many boundaries were crossed). Every timer in the game therefore updates **in the same frame**.
- `CountdownTo` returns a property whose value is `end − now` clamped at zero, updated on the aligned second ticks (so it may reach zero up to one second late). The caller disposes it.
- Game-time countdowns (level timer) use `Game`. Cooldowns that persist across sessions (daily rewards) use `Real`, and store `DateTime` UTC end times in saves.
- No server time and no anti-cheat. `IRealClock` is the seam for them.

### 9.4 Audio

- One `AudioMixer` (`Core/Audio/GameAudioMixer.mixer`, referenced by `CoreConfig`) with groups `Master` → `Music`, `Sfx`, `Ui` and exposed volume parameters `MasterVolume`, `MusicVolume`, `SfxVolume`, `UiVolume`.
- `AudioCue` ScriptableObject: `AudioClip[] Clips` (random pick), `AudioMixerGroup Group`, `float Volume`, `Vector2 PitchRange` (random), `bool Loop`. Cues live in the domain that owns them and are referenced from its config. A cue without clips, with an empty clip slot or without a group is a bug (`ArgumentException`).
  ```csharp
  public interface IAudioService
  {
      void Play(AudioCue cue);
      void PlayAt(AudioCue cue, Vector3 position);
      void PlayAttached(AudioCue cue, Transform target);
      UniTask PlayMusicAsync(AudioCue cue, float crossfadeSeconds, CancellationToken ct);
      UniTask StopMusicAsync(float fadeSeconds, CancellationToken ct);
      void SetVolume(AudioChannel channel, float volume);
  }

  public enum AudioChannel
  {
      None = 0,
      Master = 1,
      Music = 2,
      Sfx = 3,
      Ui = 4,
  }
  ```
- **Fixed authored sources.** The root prefab's `AudioSources` object has an `AudioSourceSet` (Core MonoBehaviour) holding 16 authored SFX `AudioSource`s (`Sfx Source 1..16`) and two music sources (`Music A`, `Music B`), all with `playOnAwake` off. The array length is the voice count; zero SFX sources is a bug. To change the voice count, add or remove sources on the prefab.
- **SFX.** `Play` is 2D; `PlayAt` and `PlayAttached` are 3D, and `PlayAttached` follows its target every `LateTick` (keeping the last position if the target is destroyed; sources are never parented to targets). A source becomes free again once it stops playing (it is recycled in `LateTick`). **Voice stealing:** when no source is free, a source that has already finished is reused first; only if every source is still playing is the oldest-started one stopped and reused. A looping cue passed to an SFX method throws (SFX have no stop handle).
- **Music** crossfades linearly on unscaled time between the two music sources, driven by `LateTick`, so fades survive caller cancellation and time pause. Requesting the current cue is a no-op; a newer call completes older awaits.
- `SetVolume` takes a linear 0..1 value (clamped) and sets the channel's exposed parameter in dB (`AudioVolume.ToDecibels`, floor −80 dB). A missing parameter throws. `ISettingsService` calls it.

### 9.5 Loading screen

Core owns only the contract. The screen itself is the `Loading` domain.

```csharp
namespace Core.Transitions
{
    public interface ILoadingScreen
    {
        UniTask ShowAsync(CancellationToken ct);
        UniTask HideAsync(CancellationToken ct);
    }

    public sealed class NullLoadingScreen : ILoadingScreen
    {
        public UniTask ShowAsync(CancellationToken ct);
        public UniTask HideAsync(CancellationToken ct);
    }
}
```

- `DomainRunner` depends on `ILoadingScreen` only and calls it for `Transition.Loading` runs (§4.5). The caller of `RunAsync` chooses the transition (§4.3): `Loading` for the game flow's main domains, `None` for overlays and parallel domains (D35).
- Registration follows the escape hatch (§5.2, D4): Bootstrap registers `Loading.LoadingScreen` as `ILoadingScreen` in the root. Core never references the domain. Deleting the `Loading` domain breaks the few Bootstrap lines that register and start it; replacing them with `builder.Register<ILoadingScreen, NullLoadingScreen>(Lifetime.Singleton)` keeps the game working with instant cuts.
- `ShowAsync` and `HideAsync` set a visible/hidden state and are idempotent: a call for the current state awaits the fade already running (shared through `AsyncLazy`, so several callers may await it) and starts nothing. The fade runs on the screen's lifetime, so caller cancellation never leaves it half-faded; a fade whose view goes away ends silently.
- **Input.** The first `ShowAsync` takes `ILockService<InputLockTag>.LockAll()`. The lock is released when a fade-out ends with the screen still hidden (a `ShowAsync` during the fade-out keeps it).

The `Loading` domain (main kind, `Domains/Loading/`):

- `LoadingScreen` (public, root singleton, registered by Bootstrap) holds the state and the lock. It works without a view: before the Loading scope is built (at boot) and after it is gone, `ShowAsync`/`HideAsync` only change the state and the lock.
- The scope scene `Loading.unity` authors the overlay: a `UICanvas` instance (Screen Space - Overlay, sorting order 1000, above every domain canvas) with a `CanvasGroup` and `LoadingScreenView`, a full-screen `Background` image that blocks raycasts, a `Spinner` (eight authored dots rotated by an `Animator` on unscaled time, clip `Art/Spinner.anim`), and a `Label` (`LocalizedLabel`, key `Loading/loading`: EN "Loading…", PL "Ładowanie…").
- `LoadingScreenPresenter` (`IInitializable`) attaches the view to `LoadingScreen` and detaches it on dispose. Attaching applies the current state at once (`SetVisible`), so a screen requested before the scope was built appears as soon as it is.
- `LoadingScreenView` fades its `CanvasGroup` on unscaled time over `_fadeSeconds` (0.3 s), at most 1/30 s per frame so a load hitch does not make it jump, and disables the canvas, raycasts and the spinner `Animator` while hidden. `ILoadingScreenView` exists because `LoadingScreen` is unit-tested (§6.1).
- `GameFlow` starts the domain at boot with `Transition.None` and keeps it running for the session (§4.7, §4.8).

### 9.6 Cameras (Cinemachine 3)

- The single `Camera` + `CinemachineBrain` lives on the root prefab and persists. No Core code is involved.
- `CinemachineCamera`s (virtual cameras) live in domain scope or content scenes, and domains control them through views driven by one presenter. For example `GameplayCameraView` exposes `SetFollowTarget(Transform)` and `SetDistanceScale(float)`, and `GameplayCameraPresenter` decides *when*.
- Blends are configured on the brain (default blend) or in `CinemachineBlenderSettings` assets owned by domains.
- UI canvases in domain scenes use `Screen Space - Overlay` (sorting order: domain screens 0, overlays such as Settings 100, the loading screen 1000), or `Screen Space - Camera` with the root camera assigned by the view in `Awake` via `Camera.main`. This is the one allowed lookup (§16.5).

---

## 10. Content and data

### 10.1 Content Directories

Facts verified in the Unity 6.6.3f1 Editor and a macOS standalone player (§15.1). Namespace `Unity.Loading`, modules `UnityEngine.ContentLoadModule` / `UnityEngine.CoreModule`:

- **Build (Editor):** `BuildReport BuildPipeline.BuildContentDirectory(BuildContentDirectoryParameters p)`. `BuildContentDirectoryParameters` is a struct with settable `name` (becomes `ContentDirectoryHandle.BuildName`), `outputPath`, `rootAssetPaths` (`string[]`), `compression` (`BuildCompression`, default uncompressed), `options` (`BuildContentOptions`: `None`, `CleanBuildCache`, `UseArchive`, `FailBuildWhenErrorsLogged`, ...), `extraScriptingDefines`. There is no public target field: content is built for the **active build target**. `report.summary.result` / `totalErrors` give the outcome (`BuildType.ContentDirectory`). The output is a flat folder of `*.cf` / `*.resS` files, a `<hash>.json` manifest, and `BuildManifestHash.txt`. `LoadableSceneId` and `Loadable<T>` references in the root assets are followed, so scenes and prefabs referenced from `<Name>Content.asset` are included automatically.
- **Runtime registration:** `ContentDirectoryHandle ContentLoadManager.RegisterContentDirectory(string localPath)` (synchronous), `UnregisterContentDirectory(handle)`, `ContentDirectoryHandle[] GetContentDirectories()`, `T[] GetRootAssets<T>(handle)`. `ContentDirectoryHandle` has `IsValid` and `BuildName`. Nothing is registered automatically: without registration `GetRootAssets` returns an empty array.
- **Asset references:** `Loadable<T>` is a `[Serializable]` **class** (reference equality) with `Status` (`LoadableStatus.None/Loading/Loaded/Failed`), `Target`, `LoadableObjectId`, `Load()` (forbidden), `Release()` (status returns to `None`) and `UnityEngine.Awaitable<T> LoadAsync()`, which yields `null` on failure. Convert with UniTask's `Awaitable<T>.AsUniTask()`. An `Awaitable` may be awaited only once, and calling `LoadAsync` again on a `Loadable` that is already `Loading`/`Loaded` throws.
- **Scenes:** `LoadableSceneId` is a serializable struct (scene GUID) with `IsValid`. `AsyncOperation SceneManager.LoadSceneAsync(LoadableSceneId, LoadSceneParameters)` returns **`null` when the scene is not in any registered directory**. `SceneManager.GetSceneByLoadableSceneId(id)` returns the loaded `Scene`. Unload with the normal `SceneManager.UnloadSceneAsync(scene)`. No Build Settings entry is needed. `LifetimeScope.EnqueueParent` works with scenes loaded this way (Editor and player).
- **Editor authoring:** `LoadableSceneIdEditorUtility.CreateLoadableSceneId(string path | GUID)`, `LoadableSceneIdToScene(id)` → `SceneAsset`, `LoadableSceneIdToGuid(id)`. `LoadableObjectIdEditorUtility.CreateLoadableObjectId(Object)`; `new Loadable<T>(in LoadableObjectId)`. Built-in property drawers exist for `LoadableSceneId`, `Loadable<T>`, and `LoadableObjectId`.
- **Editor Play mode:** `Loadable<T>.LoadAsync` and `LoadSceneAsync(LoadableSceneId, ...)` work **without building or registering anything** (they resolve through the AssetDatabase).
- **Player builds:** a `Loadable<T>` stored in an asset that is part of the *player build* (scene, root prefab, anything they reference directly) **fails to load**, and the build logs *"LoadableObjectId references are not supported in AssetBundle or Player builds"*. A `LoadableSceneId` in player data loads, but the build logs the same kind of error. The same references inside a content-directory root asset load fine. Serialized fields under `#if UNITY_EDITOR` do not pull their referenced assets into the player build.
- **Nested builds:** calling `BuildContentDirectory` from an `IPreprocessBuildWithReport` logs BuildReport/BuildLog errors. Do not build content from a build preprocessor.
- TMP fallback fonts are skipped in content-directory builds (§10.5).

Design:

- **One content directory per domain descriptor** (sub-domains included), whose single root asset is `<Name>Content.asset` (a `DomainContent` subclass, §4.4). It holds the scope scene, content scenes, and `Loadable<T>` references to heavy or optional assets. The directory name equals `DomainDescriptor.ContentDirectoryName`. Core's own assets (mixer, `CoreConfig`) are referenced directly by the root prefab and are part of the player build.
- **Player-build assets** (the root prefab, `CoreConfig`, domain descriptors, `Bootstrap.unity`) MUST NOT contain `Loadable<T>` or `LoadableSceneId` fields. Only content-directory assets may.
- `IContentDirectoryRegistry` (Core, root singleton):
  ```csharp
  public interface IContentDirectoryRegistry
  {
      OneOf<ContentDirectoryHandle, NotFound> Get(string name);
      OneOf<DomainContent, NotFound> GetContent(DomainDescriptor descriptor);
  }
  ```
  - `ContentDirectoryRegistry(IContentLoadManager)` implements it, with `RegisterAll(string absoluteRootPath)` and `RootFolderName = "Content"`.
  - **Players:** when the root container is built (a build callback resolves the registry, before any flow starts), it registers every subfolder of `{Application.streamingAssetsPath}/Content/` (folder name = directory name) and never unregisters them. A missing root logs a warning, an invalid handle logs an error and is skipped, a duplicate name throws. `GetContent` returns the single `GetRootAssets<DomainContent>(handle)` entry of the descriptor's directory (none = `NotFound`, more than one throws).
  - **Editor:** it registers nothing. `GetContent` falls back to `descriptor.EditorContent`, and everything loads through the AssetDatabase, so Play mode never needs a content build.
  - Domains don't register or unregister directories. Memory is managed per asset through `Release()`, and scenes through unloading.
- **Editor build step** (`Core.Editor.ContentDirectoryBuilder`): `BuildAll()` (returns `OneOf<Success, Error>`, for scripted and CLI builds) builds one directory per `DomainDescriptor` asset under `Assets/_Project/Domains/` (name = `ContentDirectoryName`, root = `EditorContent`) into `Assets/StreamingAssets/Content/<Name>` for the active build target, with `FailBuildWhenErrorsLogged`. It validates every source (valid and case-insensitively unique name, `EditorContent` set) before wiping the output folder. Entry points: the menu `Build/Content Directories`, and a `BuildPlayerWindow.RegisterBuildPlayerHandler` handler that builds content and then the player. Scripted player builds call `BuildAll()` first. `Assets/StreamingAssets/Content/` is build output and is gitignored.
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
  - `ContentLoader` reference-counts each `Loadable<T>` instance: every successful `LoadAsync` takes a reference (joining a load already in progress), every `Release` gives one back, and the last `Release` calls `loadable.Release()`. A failed, `NotFound` or cancelled load takes no reference. `Release` of an unreferenced `Loadable` is a no-op.
  - `SceneLoader.LoadAdditiveAsync` awaits the scene operation to the end, then unloads the scene and throws `OperationCanceledException` if `ct` was cancelled meanwhile, so a cancelled load never leaves a scene behind. `UnloadAsync` of a scene that is no longer loaded is a no-op (Unity unloads everything when Play mode stops).
  - `null` from `LoadAsync` maps to `NotFound`; so does a `null` `AsyncOperation` from `LoadSceneAsync` and an invalid id. A missing **scope scene** is a configuration bug, so `DomainRunner` throws.
- Configs inside a content directory reference heavy or optional assets through `Loadable<T>` fields, so they load only when needed and are released by the presenter or service that loaded them (in `Dispose`). Loading an asset is not creating an object: the loaded asset is used by authored objects (an `AudioClip`, a texture, a config), never instantiated.
- Synchronous `Loadable<T>.Load()` is **forbidden**. Async only.
- `Resources.Load` and Addressables are **forbidden**.

### 10.2 Configuration data

- **Default (≈99%): ScriptableObject configs.** The type lives in the domain's `Code/`, the asset in the domain's `Configs/`. It is referenced from the domain's `LifetimeScope` (or its content root) and registered with `RegisterInstance`. Presenters and services receive it through constructor injection as a plain object.
- Config SOs are **read-only at runtime**. Never write to them.
- Odin attributes are welcome for validation (`[Required]`, `[MinValue]`, `[ValidateInput]`) and editor UX.
- JSON data files (through `IJsonSerializer`) only for large tabular data that is impractical as SOs. This needs a reason stated in the PR.

### 10.3 Save data

- One JSON file per slot: `Saves/slot_{index}.json` below the storage root (`{persistentDataPath}` in players, `{persistentDataPath}/Editor` in the Editor, §8), written atomically.
- File shape:
  ```json
  {
    "formatVersion": 1,
    "savedAtUtc": "2026-09-28T12:00:00Z",
    "sections": {
      "gameplay": { "version": 2, "data": { "bestScore": 40, "roundsPlayed": 3 } }
    }
  }
  ```
- Each domain owns **its own section**, under a string key equal to the domain name in camelCase. The section holds a versioned **DTO** (a `sealed record` with primitive-typed properties, no Unity types). DTOs are separate from runtime models. Mapping is explicit code.
- Value-type fields of a save DTO are **nullable** (`int? BestScore`), as in settings DTOs (§10.4): Newtonsoft fills a missing field with `default` (0) without failing. The domain maps the DTO to its model, uses the field's declared default for `null` and logs one Warn listing the missing fields (`GameplayProgressService`).
- Any added, renamed, removed or reinterpreted field bumps the section's `CurrentVersion` and adds a migration step (`Docs/Rules.md` §7).
- Deleting a domain leaves an orphaned section that nothing reads. It is kept on every write. That is harmless and intentional.
- Migration: each section declares `const int CurrentVersion` and a `Migrate(JObject data, int fromVersion) → OneOf<JObject, Corrupted>` function, run on read when `version < CurrentVersion`. Chain one step per version. The migrated data is cached at the current version and persisted on the next flush.
  ```csharp
  public interface ISaveStore
  {
      int ActiveSlot { get; }

      UniTask<OneOf<Success, Error>> SelectSlotAsync(int slot, CancellationToken ct);
      OneOf<T, NotFound, Corrupted> Read<T>(SaveSection<T> section) where T : class;
      void Write<T>(SaveSection<T> section, T data) where T : class;
      UniTask<OneOf<Success, Error>> FlushAsync(CancellationToken ct);
      UniTask<OneOf<Success, Error>> DeleteSlotAsync(int slot, CancellationToken ct);
  }

  public sealed record SaveSection<T>(string Key, int CurrentVersion, Func<JObject, int, OneOf<JObject, Corrupted>> Migrate) where T : class;
  ```
- `SelectSlotAsync` loads the file into memory. `Read` and `Write` work in memory. `FlushAsync` writes only when something changed since the last load or flush. Before any slot is selected, `ActiveSlot`/`Read`/`Write` throw and `FlushAsync` is a no-op success. `CoreStartup` selects slot 0; a slot-selection UI is out of scope.
- `Read` returns `Corrupted` for a section whose migration fails or whose data does not match the DTO. An invalid section definition or data that is not a JSON object is a bug (`ArgumentException`).
- **Data written by a newer game version is never overwritten.** A section whose stored version is newer than `CurrentVersion` reads as `NotFound`, so the domain starts from its defaults. `Write` to it only changes memory (later `Read`s in the session return it), does not mark the slot dirty, and every flush writes the stored section back unchanged. `SaveStore` logs one Warn per key and slot selection. A file whose `formatVersion` is newer than `SaveStore.CurrentFormatVersion` is left untouched (no backup): the slot starts empty, `SelectSlotAsync` returns `Error`, and `FlushAsync` refuses to overwrite it.
- **Corrupted files are never lost.** A slot whose file is malformed (bad JSON, a missing or older unknown `formatVersion`, missing or broken sections) is copied to `Saves/slot_{index}.corrupted.{yyyyMMddTHHmmssfff}.json` (UTC from `IRealClock`, then `_1` … `_9` if taken), the slot starts empty, and `SelectSlotAsync` returns `Error`. If the backup fails, or the file could not be read at all, the slot starts empty and `FlushAsync` refuses to overwrite it (returns `Error`).
- Domains flush at meaningful points (level end, quitting to menu). `SaveAutoFlush` (Core entry point) also flushes when the application loses focus and on quit: players cancel the first quit request, flush, then quit; the Editor flushes on `Application.quitting`. These flushes use `CancellationToken.None` and log failures.

### 10.4 Settings

`settings.json` below the storage root (not tied to a save slot), through the same storage adapters. It holds **Core settings** and **domain-owned sections**:

```json
{
  "formatVersion": 2,
  "core": {
    "masterVolume": 1.0,
    "musicVolume": 1.0,
    "sfxVolume": 1.0,
    "uiVolume": 1.0,
    "language": "English",
    "qualityLevel": 2,
    "fullScreenMode": "FullScreenWindow",
    "resolutionWidth": 1920,
    "resolutionHeight": 1080,
    "refreshRateNumerator": 60,
    "refreshRateDenominator": 1,
    "vSync": true,
    "bindingOverridesJson": ""
  },
  "sections": {
    "gameplay": { "version": 1, "data": { "cameraDistance": 0.5 } }
  }
}
```

```csharp
public interface ISettingsService
{
    ReadOnlyReactiveProperty<SettingsState> Current { get; }

    void Apply(SettingsState state);
    T Read<T>(SettingsSection<T> section) where T : class;
    void Write<T>(SettingsSection<T> section, T data) where T : class;
    UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct);
}

public interface ISettingsLoader
{
    UniTask LoadAsync(CancellationToken ct);
}

public sealed record SettingsState(float MasterVolume, float MusicVolume, float SfxVolume, float UiVolume, Language Language, int QualityLevel, FullScreenMode FullScreenMode, Resolution Resolution, bool VSync, string BindingOverridesJson);

public sealed record SettingsSection<T>(string Key, int CurrentVersion, Func<JObject, int, OneOf<JObject, Corrupted>> Migrate, T Default) where T : class;
```

**Who edits what.**

- **Core settings** (`SettingsState`: volumes, language, graphics, binding overrides) are edited only by the **Settings** leaf domain. It calls `Apply(Current with { ... })` on every change (live preview, no revert) and `SaveAsync` when it closes.
- **Domain settings** belong to the domain that uses them. The domain declares a `SettingsSection<T>` (key = domain name in camelCase, a DTO record, `Migrate`, `Default`), reads and writes it through a domain service, and edits it in **its own UI**. The Settings domain never shows them. Deleting the domain leaves an orphaned section, which is kept unchanged. Example: Gameplay's camera distance (§13).
- Value-type fields of a settings DTO are **nullable** (`float? CameraDistance`), as in Core's own DTO: Newtonsoft fills a missing constructor parameter with `default` (0) without failing, so a non-nullable field would turn a missing value into a silent 0. The domain service treats `null` like an invalid value: it uses the default and logs one Warn.
- Any added, renamed, removed or reinterpreted field of a section bumps its `CurrentVersion` and adds a migration step (`Docs/Rules.md` §7).

Behaviour:

- `LoadAsync` runs once, from `CoreStartup`, before the first domain. It applies the loaded state.
  - File `NotFound` → defaults (from `CoreConfig` and the current graphics device) are applied and saved. Malformed JSON or a missing or older unknown `formatVersion` → Warn, defaults, overwritten (settings are cheap; no backup). A read `Error` → Warn, defaults, the file is **not** overwritten at load.
  - A `formatVersion` newer than `SettingsService.CurrentFormatVersion` → Warn, defaults; the file is kept unchanged and every later `SaveAsync` returns `Error` without writing.
  - A `formatVersion` 1 file (the old flat shape without sections) is read into `core` and rewritten as format 2.
  - Per field: missing → default silently; invalid (volume outside 0..1, unsupported language, quality out of range, bad resolution or fullscreen mode, unparsable bindings) → default and one Warn listing the fields; the file is re-saved when anything was defaulted.
- `Apply` validates first and throws on an invalid state (bug; nothing applied). It then applies binding overrides (only when the JSON changed), volumes (`IAudioService.SetVolume`), language, quality, vsync and screen, and sets `Current`. `Apply` never persists.
- `Read` never fails: a missing section returns `Default`; a corrupted one (bad envelope, failed migration, data not matching the DTO) returns `Default` and logs one Warn per key. A section with a version newer than `CurrentVersion` returns `Default` and logs one Warn per key; `Write` to it only changes memory (later `Read`s return it) and `SaveAsync` writes the stored section back unchanged. Sections are parsed lazily, so one bad section never resets Core settings or other sections. A migrated section is cached at the current version only when the migrated data deserializes; otherwise the stored data stays unchanged, so a fixed migration can read it later. `Read` before `LoadAsync` returns `Default`.
- `Write` stores in memory; `SaveAsync` persists Core settings and every section, including unknown and newer ones. An invalid section definition or data that is not a JSON object is a bug.
- Graphics go through `IGraphicsDevice`; setters only touch `QualitySettings`/`Screen` when the value differs.

### 10.5 Localization (custom; Unity Localization is not used)

- `Language` enum in Core (`None = 0`, `English = 1`, `Polish = 2`). `LanguageExtensions` gives `GetCulture()` (used by `Format`) and `GetNativeName()` (for language pickers). `CoreConfig` lists the supported languages and the default. Adding a language means a new enum value, a column field on `LocalizationEntry`, and the switch arms in `LocalizationEntry.GetText` and `LanguageExtensions` (`LanguageTests` fail for a value without them).
- **Tables:** `LocalizationTable` ScriptableObject (`TableName` + entries; Odin table: rows = keys, columns = languages, one serialized string field per language, plain Unity serialization).
  - One table per domain that needs its own strings: `<Name>Text.asset` in the domain folder (outside `Code/`). The domain scope registers it explicitly: `builder.RegisterLocalizationTable(_text)` in `ConfigureDomain`. The registration is an entry point that adds the table in `Initialize` (before any `Start`) and removes it when the scope is disposed. A domain without its own strings (Settings, Pause) has no table.
  - Strings used by more than one domain live in the **Shared** table: `Shared/UI/Localization/SharedText.asset`, keys in the public `Shared.UI.Localization.SharedText`. The root registers it from `CoreConfig.SharedText`.
  - Duplicate table names throw; duplicate keys or an empty table name are bugs (`ArgumentException`).
- **Keys:** generated constants. `Core.Editor.Localization.TextKeyGenerator` (menu `Tools/Localization/Generate Text Keys`, on table save, and on table move or delete) emits `<TableName>Text.g.cs`:
  ```csharp
  using Core.Localization;

  namespace Gameplay
  {
      internal static class GameplayText
      {
          internal const string TableGuid = "50c7eeabe52f6475cbbefcd61b61772e";

          public static readonly TextKey YouWon = new("Gameplay", "you_won");
      }
  }
  ```
  - Output folder: the nearest `Code/` folder at or above the table's folder, plus the table's path below that module (`Domains/Gameplay/GameplayText.asset` → `Domains/Gameplay/Code/GameplayText.g.cs`; `Shared/UI/Localization/SharedText.asset` → `Shared/UI/Code/Localization/SharedText.g.cs`). The namespace follows §3.3. The class is `internal` under `Assets/_Project/Domains/` and `public` elsewhere.
  - Keys are lower snake_case (→ PascalCase constants), table names PascalCase. Name collisions and the reserved key `table_guid` are errors: nothing is written.
  - Generated files contain no comments. `TableGuid` marks the file as generated and names its table; the generator deletes a `.g.cs` whose table moved elsewhere or no longer exists. Never edit a `.g.cs` by hand.
  A typo is then a compile error, not a silent miss.
- Service:
  ```csharp
  public interface ILocalizationService
  {
      ReadOnlyReactiveProperty<Language> Current { get; }

      string Get(TextKey key);
      string Format(TextKey key, params object[] args);
      void SetLanguage(Language language);
  }
  ```
  A missing key (or unregistered table) returns `"{table}/{key}"` and logs Warn once per key. An empty translation falls back to the default language and logs Warn once per key and language. `SetLanguage` is called only by `SettingsService.Apply`; an unsupported language throws, the same language is a no-op.
- **Static labels:** `LocalizedLabel` (a Core MonoBehaviour, i.e. a view) holds `[SerializeField] TextKey _key` + `[SerializeField, Required] TMP_Text _text`, and exposes `TextKey Key` and `SetText(string)`. It has no logic and no injection. `TextKeyDrawer` shows a dropdown of every table key.
- **Binder:** `LocalizedLabelBinder` is an entry point that `DomainRunner` registers in every domain scope. On start it collects every `LocalizedLabel` (inactive included) under the scope scene's roots and in every scene `DomainSceneSet` loads later, sets their text, and re-sets it whenever `Current` changes. It disposes its subscription with the scope. The root scope has its own binder for root-prefab labels.
- **Dynamic text** is set by presenters through `ILocalizationService`, re-applied when `Current` changes (`MainMenuPresenter` version label).
- **Fonts:** `Shared/UI/Fonts/LiberationSans Latin SDF.asset` is the TMP default font: a static atlas covering ASCII, Latin-1 and Polish letters, with TMP's dynamic LiberationSans font as fallback. Content-directory builds skip fallback fonts, so in players every character a supported language needs MUST be in the static atlas.

---

## 11. Coding rules summary

`Docs/Rules.md` (no runtime object creation, no comments, code layout, commits, packages, persisted data) and `Docs/Coding Conventions.md` apply in full. In addition:

- Conventions: Allman braces, `_camelCase` private fields, member ordering as specified (properties before fields, mutable fields before readonly fields, private methods before `Dispose`), `[SerializeField] private` or `[field: SerializeField]` properties, no public fields, enums with explicit values and `0 = None`.
- No comments of any kind in code, samples or generated files. The only exceptions are the `// Arrange`, `// Act`, `// Assert` markers in tests (§14.3) and tool-owned generated files (`GameInput.cs`).
- Classes are `sealed` by default. Domain internals are `internal` (§4.1).
- DI classes use **explicit constructors** that assign `private readonly` fields (matches the conventions' field naming). **Primary constructors are only for records** (§16.1).
- `var` per the conventions (use it when the type is obvious).
- **Block-scoped namespaces for all code** (§2.3, §15.5).
- No LINQ in per-frame code (`ITickable` paths). LINQ is fine elsewhere.
- No `static` mutable state except `Log` configuration and `BootMode`.
- Every `IDisposable` created is disposed by its owner. Every subscription lands in a `DisposableBag`.
- Forbidden APIs in game code: `Resources.Load`, `Debug.Log*` outside `Log`, `[Inject]`, `async void`, C# `event`, synchronous `Loadable<T>.Load()`, `Object.Instantiate`/`InstantiateAsync`, `new GameObject`, `AddComponent`, `Find*` (except §5.2).

---

## 12. Lifecycle walkthrough (normal boot)

1. Unity loads `Bootstrap.unity`. `BootMode.Current` is resolved (`Normal`). VContainer instantiates the `RootLifetimeScope` prefab (via `VContainerSettings`) before any scene scope.
2. Root `Configure` registers `ScopeRef(root, 0)`, runs `CoreInstaller.Install`, registers the domain entries, and registers `GameFlow`. When the container is built, the content registry registers the content directories (players only).
3. `GameFlow.StartAsync(ct)` awaits `CoreStartup.RunAsync(ct)`: `SettingsService` loads and applies settings (volumes, language, graphics, bindings), then `SaveStore` selects slot 0.
4. `GameFlow` starts `LoadingDomain.RunAsync(args, None, ct)` without awaiting it. The runner takes the load gate and loads `Loading.unity`; `LoadingLifetimeScope` builds at depth 1.
5. `GameFlow` calls `MainMenuDomain.RunAsync(args, Loading, ct)`, and `DomainRunner` then:
   1. checks the guards and shows the loading screen (no view yet: `LoadingScreen` records the state and locks input; the view shows it at full opacity when `LoadingScreenPresenter` attaches it),
   2. waits for the load gate, enqueues the root as parent plus the args, content root, completion, and label binder,
   3. loads `MainMenu.unity` from the MainMenu content directory; `MainMenuLifetimeScope` builds as a child of root (depth 1),
   4. releases the gate, waits one frame and for pending content loads, hides the loading screen (fade-out, then the input lock is released), and awaits the completion.
6. The user clicks Play. `MainMenuPresenter` calls `Complete(new MainMenuResult.Play())`. The runner shows the loading screen (fade-in over the menu), then its `finally` disposes the scope (presenters dispose, subscriptions end, input handles pop, the table is removed) and unloads the scene. `RunAsync` returns `Play` with the screen still up.
7. `GameFlow` runs `GameplayDomain.RunAsync(args, Loading, ct)`: the screen is already up, the Gameplay scope builds, its flow presenter loads the room, and only then does the screen fade out. Quitting to the menu is the same switch in the other direction.

---

## 13. Sample vertical slice (ships with the template; deletable)

Purpose: most patterns in this document exercised once, as small as possible. Not in the sample: `Loadable<T>` loaded through `IContentLoader` (only `ContentLoaderTests` shows it), music (`PlayMusicAsync`), `PlayAttached`, ending a parallel domain early through a linked `CancellationTokenSource` (§4.7) and domain-local lock services (§9.2).

| Domain | Kind | Content | Demonstrates |
|---|---|---|---|
| `MainMenu` | main | Canvas with title, Play / Settings / Quit, version label | view outputs through R3, `DomainCompletion`, launching a leaf domain with `SubscribeAwait(Drop)`, its own `MainMenuText` table, dynamic localized text |
| `Gameplay` | main | a room (content scene `Gameplay_Room`: geometry, light, spawn point, 8 collectibles each with its own authored pickup effect), a capsule player, Cinemachine follow camera, HUD (score + countdown), win/lose panel | content scenes through `DomainSceneSet`, input handlers + `IFixedTickable` movement, MLock (pause session, round end), `TimerService` countdown (game clock), `ScoreModel` with `ReadOnlyReactiveProperty`, save section `gameplay` v2 with a v1 migration (best score, rounds played; nullable DTO fields mapped to a `GameplayProgress` model), `AudioCue`s (collect/win/lose), authored per-item effects instead of spawning, a domain-owned settings section (camera distance) |
| `Gameplay/Pause` | sub | overlay: Resume / camera distance slider / Settings / Quit to menu | sub-domain folder with an asmref into the parent's assembly, `RegisterSubDomain`, `ITimeService.Pause()`, input map stack push, resolving a parent service (`GameplaySettingsService`) to edit the domain's own setting, returning a union to the parent (`Resume`, `OpenSettings`, `QuitToMenu`) |
| `Loading` | main | loading screen: full-screen background, animated spinner, "Loading…" label; fades in and out | a long-lived domain running in parallel with the flow (§4.7, D15) that ends only through cancellation, a Core contract implemented by a domain and registered by Bootstrap (`ILoadingScreen`, §5.2, D4), `IInitializable` attach, an `Animator` on unscaled time, its own `LoadingText` table |
| `Settings` | leaf | overlay: volume sliders, language selector (previous/next), back | leaf domain reused from MainMenu (depth 2) and Gameplay (depth 2), editing Core settings through `ISettingsService` (`Apply`, `SaveAsync`), live language switch, an authored `SelectorView` instead of a dropdown |

Results: `MainMenuResult = Play | Quit`. `GameplayResult = Won | Lost | QuitToMenu`. `PauseResult = Resume | OpenSettings | QuitToMenu`. `SettingsResult = Closed`. `LoadingResult = Stopped` (never completed; the domain ends through cancellation).

Gameplay flow:

- `GameplayFlowPresenter` loads the level's content scene (`GameplayArgs.LevelIndex` indexes `GameplayContent.EnvironmentScenes`), places the player, points the camera, starts `CollectiblesPresenter`, and awaits `RoundService.RunAsync`, which returns the `GameplayResult` itself. On Won/Lost it plays the cue, records and saves progress, shows the result panel, waits for Continue, then completes the domain.
- **Pickups:** on touch, `CollectiblesPresenter` hides the collectible (trigger and visual off), plays the cue, adds the points, and awaits the collectible's own `PickupEffectView.PlayAsync(ct)`. Nothing is spawned.
- **Pause:** `PlayerInputHandler` raises `PauseRequests.Request()`. `PauseFlowPresenter` holds a time pause and a `Movement` lock for the whole session and loops: run Pause, save Gameplay's settings, and on `OpenSettings` run Settings and then Pause again. Because of the depth limit, **Pause returns `OpenSettings` and Gameplay launches Settings**. The Pause sub-domain itself pauses time, pushes `Ui`, and maps UI/Cancel (Escape) to Resume.
- **Camera distance setting:** `GameplaySettings.Section` (`gameplay` v1, `GameplaySettingsDto(float? CameraDistance)` 0..1, default 0.5; missing or out of range → default + Warn), owned by `GameplaySettingsService` (reactive, validated, saved only when changed). `PausePresenter` edits it; `GameplayCameraPresenter` scales the camera's authored follow offset with it, also while paused.

---

## 14. Testing

### 14.1 Libraries

- NUnit (Unity Test Framework 1.8), **NSubstitute 6.2** (Editor only; uses Reflection.Emit, fine in EditMode), **AwesomeAssertions 9.6** (`using AwesomeAssertions;`, API as FluentAssertions 7: `x.Should().Be(...)`).
- These DLLs are `autoReferenced=false`. Test asmdefs reference them explicitly (§3.3). Runtime asmdefs MUST NOT reference them. `TestOnlyPluginImporterEnforcer` keeps them Editor-only (§15.4).

### 14.2 What to test

- **EditMode, per assembly** (`Core.Tests`, `Gameplay.Tests`, …): services, models, union-returning logic, save and settings migrations, JSON round-trips, `TimerService` alignment (with fake clocks), `DomainCompletion`, `DomainRunner` (guards, cancellation, entry-point failure, teardown, the loading-screen handoff), localization lookup, and the pure parts of editor tools (the text-key generator).
- Presenters: only when they carry real logic (`PauseFlowPresenter`). Thin wiring presenters are verified in Play mode instead. Introduce a view interface only for a presenter that is tested (§6.1).
- Objects under test are **constructed by hand**, never resolved from a container. The exceptions are tests whose subject is the registration or the scope build itself (`RegisterDomain`, `DomainLifetimeScope`, the runner with real scopes from `TestUtils`).
- **PlayMode smoke test** (`Bootstrap.PlayModeTests`, one test):
  - `TestBootSetup` (`IPrebuildSetup`/`IPostBuildCleanup`) deletes `BootMode.TestStorageRoot` (`{project}/Temp/PlayModeTestStorage`) and sets `BootMode.TestKey` before Play; afterwards it erases the key and deletes the folder, whatever the result. The root boots in `Test` mode: no flow entry point, storage rooted in the temp folder.
  - The test is the flow: it awaits `CoreStartup.RunAsync` (so the real settings load and slot selection run against the temp folder, which proves the storage root), then for each root-registered `IDebugRunnableDomain` runs `RunDebugAsync` with a token linked to the root's lifetime.
  - Per domain it asserts that the scope scene loaded, the scope built at depth 1 under the root, and that after cancelling, the run was cancelled, the scope scene and every content scene unloaded, and the scope was disposed.
  - It asserts that nothing under the real `persistentDataPath` changed.
  - The `Loading` domain is root-registered and debug-runnable, so it is smoke-tested like the others.
  - Sub-domains are not smoke-tested (they cannot be debug-run).

### 14.3 Style

- Class `<Subject>Tests`. Method `MethodName_Condition_ExpectedResult`. Body sections `// Arrange`, `// Act`, `// Assert`.
- Async tests are `public async Task Name()` (UTF 1.8 supports them). Inside, `await` UniTasks directly or via `.AsTask()`. Await a faulted `UniTask` before the test ends, so no unobserved exception surfaces in a later test.
- Union assertions come from `TestUtils` (AwesomeAssertions extensions over `IOneOf`):
  ```csharp
  result.Should().BeCase<NotFound>();
  var won = result.Should().BeCase<GameplayResult.Won>().Which;
  won.Score.Should().Be(10);
  ```
- Fakes: prefer NSubstitute. Hand-written fakes that are reused go in `TestUtils`: `FakeClock` (both clocks, settable `UtcNow`, `Advance`), `InMemoryFileStorage` (`Files`, `WrittenPaths`, honours cancelled tokens), and the domain scopes `EmptyDomainScope`, `FailingDomainScope`, `CancellingDomainScope`, `UnresolvableDomainScope`. MonoBehaviours used by tests live in `TestUtils` because a MonoBehaviour declared in an Editor-only test assembly cannot be added to a GameObject; their files are not named `*LifetimeScope.cs` (§5.3).

### 14.4 Not in scope now

Architecture-rule tests (asmdef reference validation), CI, and AI-agent tooling (CLAUDE.md, scaffolders, the Unity CLI/`com.unity.pipeline` workflow) are **deferred** to a separate design session. Do not implement them.

---

## 15. Spike results (verified 2026-09-28, Unity 6000.6.3f1, macOS)

All spikes ran in a throwaway `Assets/_Spikes` assembly (deleted afterwards) in the live Editor, plus a macOS standalone player build for 15.1. ✅ = assumption verified. ⚠️ = assumption failed or was only partly true; the stated fallback or change was taken. **All spikes are resolved**: their outcomes are part of the design above and implemented.

| # | Assumption | Status | Findings |
|---|---|---|---|
| 15.1a | Scenes loaded via `LoadableSceneId` need no Build Settings entry. | ✅ | Editor and player: `SceneManager.LoadSceneAsync(LoadableSceneId, new LoadSceneParameters(LoadSceneMode.Additive))` loaded a scene absent from Build Settings (player had only the boot scene). |
| 15.1b | In the Editor, `Loadable<T>`/`LoadableSceneId` load without building directories. | ✅ | Editor Play mode loads both through the AssetDatabase with nothing built or registered. `GetRootAssets` returns nothing until a directory is registered, so the Editor gets content roots from the editor-only `DomainDescriptor.EditorContent` (§4.4, §10.1). |
| 15.1c | Output lives in `StreamingAssets/Content/<Name>`; when to call `RegisterContentDirectory`. | ✅ with changes | `Assets/StreamingAssets/Content/<Name>` is copied into the player and `RegisterContentDirectory(Path.Combine(Application.streamingAssetsPath, "Content", name))` works synchronously; register once at boot, before anything loads. **But:** (1) a `Loadable<T>` in a player-build asset fails to load at runtime and both `Loadable<T>` and `LoadableSceneId` in player data log build errors, so descriptors hold no `LoadableSceneId`; the scope scene lives on the content root `DomainContent`, read with `GetRootAssets<DomainContent>(handle)` (§4.4, §4.5, §10.1). (2) Building content from `IPreprocessBuildWithReport` works but logs BuildReport/BuildLog errors, so the build step uses a menu + `BuildPlayerWindow.RegisterBuildPlayerHandler` instead. Unregistered ids: `LoadSceneAsync` returns `null`, and `Loadable<T>.LoadAsync` yields `null` with `Status = Failed`. |
| 15.1d | `LoadableSceneId` can be authored from a `SceneAsset` in the inspector. | ✅ | Built-in `LoadableSceneIdDrawer`, `LoadableDrawer`, and `LoadableObjectIdDrawer` implement both `OnGUI` and `CreatePropertyGUI`, so they also work under Odin (checked by reflection). Code: `LoadableSceneIdEditorUtility.CreateLoadableSceneId(path)`, `LoadableObjectIdEditorUtility.CreateLoadableObjectId(obj)` + `new Loadable<T>(in id)`. |
| 15.1e | `Loadable<T>.LoadAsync` converts to UniTask. | ✅ | It returns `UnityEngine.Awaitable<T>`; `loadable.LoadAsync().AsUniTask()` (UniTask's `UnityAwaitableExtensions`). Scene ops: `asyncOperation.ToUniTask()`. Cancellation handling lives in the loaders (§10.1). |
| 15.1f | `LifetimeScope.EnqueueParent` works with `LoadSceneAsync(LoadableSceneId, ...)`. | ✅ | The child scope in the loaded scene got the enqueued parent and resolved a parent registration, in both Editor and player. |
| 15.2 | `OneOf.SourceGenerator` works as a Roslyn analyzer on 6.6 with `-langversion:12`. | ✅ | The DLL carries the `RoslynAnalyzer` label and appears as `<Analyzer>` in the generated csproj. `[GenerateOneOf] partial class X : OneOfBase<X.A, X.B, X.C>` with nested `readonly record struct` cases (including a parameterless `readonly record struct QuitToMenu;`) compiles: implicit conversions and `Match` work. |
| 15.3 | MLock 2.1.0 compiles on 6.6 and its debug windows work. | ✅ | Compiles with no warnings. `Window/MLock/Locks Debug` and `Window/MLock/Services Debug` open and repaint without errors in Play mode. A service shows up only after `.WithDebug()` (§9.2). No submodule changes. |
| 15.4 | Restoring R3's dependencies causes no version conflicts. | ✅ / ⚠️ | ✅ A clean recompile after restore shows no errors or assembly-version conflicts. ⚠️ The test-only DLLs (NSubstitute, Castle.Core, AwesomeAssertions, System.Diagnostics.EventLog) were copied into Mono player builds. Their `.meta` files are regenerated on restore, so the fix is code: `Core.Editor.TestOnlyPluginImporterEnforcer` runs on load and after such a DLL is imported, and calls `SetCompatibleWithAnyPlatform(false)`, `SetCompatibleWithEditor(true)`, `SaveAndReimport()` when needed. Changing the importer in `OnPreprocessAsset` did **not** persist. |
| 15.5 | Per-asmdef `csc.rsp` (`-langversion:12`, `-nullable:enable`) is honored and reflected in Rider's csproj. | ✅ / ⚠️ | ✅ Collection expressions compile; `string?` dereference gives warning CS8602. The Rider csproj of the assembly shows `<LangVersion>12</LangVersion>` and `<Nullable>enable</Nullable>`. Scripts compiled through an `.asmref` get the referenced asmdef's `csc.rsp`. ⚠️ **File-scoped namespaces break `MonoScript` → class association** (`GetClass()` returns null; components and ScriptableObjects get a broken `m_Script`). Hence block-scoped namespaces (§2.3, §16.2). |
| 15.6 | `readonly record struct LogTag` serializes in Unity. | ⚠️ fallback | Not serialized, with or without `[field: SerializeField]` on the positional parameter. `LogTag` is a plain `[Serializable] struct` with `[SerializeField] private string _name` (§7.7). |
| 15.7 | R3 `SubscribeAwait` + UniTask interop (§6.3). | ✅ | Both an `async ValueTask` handler and a UniTask handler wrapped with `.AsValueTask()` compile and behave correctly at runtime. `AwaitOperation.Drop` ignored clicks while a handler ran, and disposing the subscription cancelled the in-flight handler's `ct` without logging. **Chosen pattern:** `SubscribeAwait((_, ct) => XAsync(ct).AsValueTask(), AwaitOperation.Drop)` with `async UniTask XAsync(CancellationToken ct)` (§6.3, §16.8). |

---

## 16. Derived rules (decided by the architect, not explicitly discussed with the owner — owner may override)

1. DI classes use explicit constructors with `private readonly _fields`. Primary constructors are only for records (reason: the conventions' field naming and ordering).
2. Block-scoped namespaces everywhere, because file-scoped ones break `MonoScript` class association in Unity 6.6 (§15.5).
3. Plain C# `event`s are not used; R3 everywhere (owner approved "R3 for anything subscribable", and this is the literal reading).
4. The project-wide Input Actions reference in Input System settings is removed in favour of the generated `GameInput` instance, to avoid two copies of the actions.
5. `Camera.main` is allowed only in views, for assigning a canvas's world camera.
6. Pause cannot launch Settings (depth limit). It returns `OpenSettings` to Gameplay instead.
7. The save slot is selected at boot (slot 0) by `CoreStartup`. A slot-selection UI is out of scope.
8. Async handlers on view outputs use `SubscribeAwait((_, ct) => XAsync(ct).AsValueTask(), AwaitOperation.Drop)` with `XAsync` returning `UniTask` (chosen in the §15.7 spike, §6.3). `async UniTaskVoid` handlers with busy flags are not used.
9. `LogTag` lives on descriptors, so the runner can tag its own logs per domain.
10. `IApplicationService` wraps `Application.Quit`/version for testability.
11. Domain internals are `internal`. Only a main or leaf domain's entry class, args, result, and descriptor are public; a sub-domain is entirely internal.
12. Domain entry classes are registered `Lifetime.Scoped`, so they always get the resolving scope's `ScopeRef` (§4.8).
13. An entry point that throws (other than cancellation) fails its domain's run: `RunAsync` throws to the launcher after teardown (§4.5).
14. The flow entry point owns Core startup (`CoreStartup.RunAsync`), not a separate Core entry point (§4.8).
15. `CoreInstaller.Install` receives root-prefab scene objects and the storage root as parameters; `CoreConfig` holds asset references only (§8).
16. A corrupted save file is backed up and never overwritten; a corrupted settings file is replaced by defaults (§10.3, §10.4). Neither rule applies to a newer `formatVersion`, which is never overwritten (D48).
17. Domain settings sections live under `sections` in `settings.json`, not next to `core`, so a domain key can never collide with Core's (§10.4).
18. A `Transition.Loading` run reveals when its domain is ready: one frame after the scope build (so its entry points have started) and after the content scenes they load through `DomainSceneSet`, not right after the build. Otherwise Gameplay's room would pop in during the fade-out (§4.5).
19. Bootstrap registers exactly one `ILoadingScreen` (the `Loading` domain's, or `NullLoadingScreen`); Core registers no default for Bootstrap to override (§5.2, §9.5).
20. An internal interface that a test must fake is faked by hand in the test assembly: NSubstitute cannot proxy internal types without `InternalsVisibleTo("DynamicProxyGenAssembly2")`, which §3.3 does not allow (`LoadingScreenTests`).

Conflicts with `Docs/Coding Conventions.md`: none. The conventions' events section does not apply (§16.3).

---

## 17. Decision log

Design interview, 2026-09-28:

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
| D24 | One presenter drives many item views. (The pooled view factory of the interview is replaced by D42.) | — |
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
| D35 | Transitions chosen per `RunAsync` call; default none. | Parallel domains must not all fade. (Superseded in part by D51: the transition is now the loading screen.) |
| D36 | Audio: mixer groups, `AudioCue` SOs per domain, music crossfade. SFX voices are fixed authored sources (D42). | — |
| D37 | Settings in their own `settings.json` (including rebinds). | — |
| D38 | `= null!` + Odin `[Required]` for serialized references. | — |
| D39 | Dependencies installed first; implementation after the design was approved. | Owner. |

Owner decisions during implementation, 2026-09-29:

| # | Decision | Rationale / owner note |
|---|---|---|
| D40 | Code layout: every module keeps scripts, asmdefs and `csc.rsp` in `<Module>/Code/`; tests in `<Module>/Tests/`; non-code assets outside `Code/` (§3, `Docs/Rules.md` §4). | Separates code from content. |
| D41 | A sub-domain is one self-contained folder `Domains/<Main>/<Sub>/` with its own `Code/` and a `<Main>.<Sub>.asmref` into the main assembly. | A sub-domain must not be split across folders (P2). |
| D42 | No runtime object creation at all: no `Instantiate`/`InstantiateAsync`, `new GameObject`, `AddComponent`, spawning factories or pools. Core has no `ViewFactory`/`ViewPool`; pickup effects are authored per collectible; audio uses fixed authored sources with oldest-voice stealing (§6.6, §9.4, `Docs/Rules.md` §1). | Everything that exists at runtime is authored. |
| D43 | Domain-owned settings: `settings.json` format 2 with a `core` object and per-domain versioned `sections`. The Settings domain edits Core settings only; a domain edits its own settings in its own UI (demo: Gameplay camera distance in the Pause overlay) (§10.4). | Adding a domain setting must not touch Core; deleting a domain removes its settings UI. |
| D44 | The PlayMode smoke test runs the real Core startup against a temporary storage folder (`BootMode.TestStorageRoot`), never the developer's files (§14.2). | Tests should exercise a real boot without side effects. |
| D45 | The Editor stores saves and settings in `{persistentDataPath}/Editor`, apart from players (§8). | An Editor session must not change what a player on the same machine loads. |
| D46 | No comments in code, including samples and generated files; generated key files carry a `TableGuid` constant instead of a marker comment (§10.5, `Docs/Rules.md` §2). | Owner rule. |
| D47 | `Docs/Rules.md` holds the standing owner rules and overrides this document and the conventions where they conflict. | One short list of rules that always apply. |
| D48 | Save and settings data written by a newer game version is never overwritten: a newer section gives the domain its defaults in memory and is written back unchanged (one Warn per key); a newer `formatVersion` leaves the whole file untouched (§10.3, §10.4). | Playing an older build must not destroy newer progress. |
| D49 | Value-type fields of save DTOs are nullable with explicit defaults applied on read, like settings DTOs (§10.3). | A missing field gets its intended default, not a silent 0. |
| D50 | Adding, renaming, removing or reinterpreting a persisted field bumps the section version and adds a migration (`Docs/Rules.md` §7). | Stored data stays readable across versions. |
| D51 | 2026-09-29: A `Loading` domain replaces the Core fade overlay. It runs alongside the flow for the whole session and owns an authored loading screen (localized label, animated spinner, fades). Core keeps only `ILoadingScreen` + `NullLoadingScreen`; Bootstrap registers the domain's implementation (D4). `Transition` is `None | Loading`. A `Loading` run covers the end of its domain before the teardown and leaves the screen up when it returns; the next `Loading` run reveals once its domain is ready (scope built, entry points started, their content scenes loaded); a run that throws reveals after its teardown. Input stays locked while the screen is visible (§4.5, §4.7, §9.5). | Switching MainMenu ↔ Gameplay showed a hard cut: the old fade covered only the loading of the next domain, not the teardown of the previous one. Deleting the domain must leave a working game with instant cuts (P2). |

---

## 18. Implementation status

The design is implemented (EditMode and PlayMode tests pass). There is no pending implementation order. New features follow the existing code (§0.4) and keep this document in sync (Status line).
