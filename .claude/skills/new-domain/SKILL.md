---
name: new-domain
description: Step-by-step checklist for creating a new domain (main, leaf or sub-domain) in this Unity project - folder layout, asmdef/asmref and csc.rsp, entry class with RunAsync, Args record, OneOf Result union, DomainDescriptor and DomainContent assets, DomainLifetimeScope, the scope scene, registration with RegisterDomain/RegisterSubDomain in the launcher, and its test assembly. Use when adding a new feature that has its own lifetime (a screen, mode, overlay, level, minigame), when turning part of a domain into a sub-domain, or when renaming/removing a domain.
---

# Creating a domain

Paths are relative to `src/Assets/_Project/`. Rules: `docs/Rules.md` (Boundaries, Layout, DI and lifecycle). Model: `docs/Architecture.md`.

## 0. Decide

- Own lifetime (starts, owns scenes/UI/state, ends with a result)? If not, it is a feature inside a domain (`domain-feature`) or Core (`core-service`).
- Kind: **main** (launched by the game flow; own asmdef), **leaf** (reusable overlay/tool launched by several launchers; own asmdef; references only Core and Shared), **sub** (used only by one main domain; folder `Domains/<Main>/<Sub>/`, compiled into the main assembly through an asmref, all types `internal`, may inject the parent's services).
- Depth limit: a sub or leaf domain (depth 2) cannot launch anything. It returns a result case (for example `OpenSettings`) and its launcher launches the next domain.

## 1. Code (`Domains/<Name>/Code/`)

Copy the skeletons from `templates.md`:

- [ ] `<Name>.asmdef` + `csc.rsp` (sub-domain: only `Code/<Main>.<Sub>.asmref`, no asmdef, no csc.rsp).
- [ ] `<Name>Domain.cs` (public; implements `IDebugRunnableDomain` unless sub), `<Name>Args.cs` (sealed record with `CreateDebug()` under `#if UNITY_EDITOR`, unless sub), `<Name>Result.cs` (`[GenerateOneOf]` named union, nested cases).
- [ ] `<Name>DomainDescriptor.cs` (public, `[CreateAssetMenu(menuName = "Domains/<Name> Descriptor")]`), `<Name>Content.cs` (internal `DomainContent`).
- [ ] `<Name>LifetimeScope.cs` (internal `DomainLifetimeScope`, overrides `ConfigureDomain` only). Before Unity first imports it, write its `.meta` with a fresh GUID, or re-check the file content after import: VContainer's template overwrites new `*LifetimeScope.cs` files.
- [ ] `LogTags.cs`; `AssemblyInfo.cs` with `InternalsVisibleTo("<Name>.Tests")` if it gets tests.
- [ ] At least one flow presenter that owns `DomainCompletion<<Name>Result>` (see `domain-feature`).
- [ ] Visibility: only entry class, Args, Result and descriptor are public (sub-domain: nothing public).

## 2. Assets (through the Editor, see `assets.md`)

- [ ] `Domains/<Name>/Scenes/<Name>.unity`: the one scope scene, with a root GameObject holding `<Name>LifetimeScope` (`autoRun` on, parent empty) and the authored views it serializes.
- [ ] `Domains/<Name>/<Name>Content.asset` with `ScopeScene` set to that scene.
- [ ] `Domains/<Name>/<Name>DomainDescriptor.asset`: `ContentDirectoryName` = `<Name>` (unique), `LogTag` name = `<Name>`, `EditorContent` = the content asset.
- [ ] Optional: `<Name>Text.asset` localization table (`ui-views`), `Configs/`, `Prefabs/`, `Art/`.

## 3. Register and launch

- [ ] Every scope that launches the domain registers it itself: `builder.RegisterDomain<<Name>Domain>(_descriptor)` (sub-domain: `RegisterSubDomain`) with a `[SerializeField]` descriptor field on that scope, assigned in the Editor.
- [ ] Main domains are registered in the root scope by the game's `GameModule` subclass (base: `Bootstrap/Code/GameModule.cs`; the asset is assigned on the root prefab), and awaited by the module's `IMainFlow`. See `launching-domains`.
- [ ] The launcher's asmdef references the new assembly (never main → main).
- [ ] Launch: `await _domain.RunAsync(new <Name>Args(), Transition.None | Transition.Loading, ct)` and `Match`/`Switch` the result (`launching-domains`).

## 4. Tests and verification

- [ ] `Domains/<Name>/Tests/<Name>.Tests.asmdef` + `csc.rsp` for any real logic (`writing-tests`).
- [ ] Refresh, recompile, empty error console, run EditMode tests and the PlayMode smoke test (`Bootstrap/Tests/DebugRunnableDomainTests.cs` debug-runs every root-registered `IDebugRunnableDomain`). Commands: `docs/UnityCli.md`.
- [ ] Press Play with the scope scene open: the play-from-any-scene hook debug-runs the domain (root-registered domains only; sub-domains and leaves registered below the root cannot be debug-run).

## Removing or renaming a domain

Delete or rename the folder with its `.meta` files (through the Editor), fix the compile errors in its launchers, and drop the descriptor field from the launcher scope. Orphaned save/settings sections are harmless. Grep `.claude/skills/` for the old name.

## Pitfalls

- A scope scene played or loaded without the runner throws "was not built by DomainRunner": expected.
- A second concurrent run of the same domain type throws: one instance per descriptor type.
- `LoadableSceneId`/`Loadable<T>` belong on `<Name>Content`, never on the descriptor (player-build asset).
- Namespaces follow folders without `Code` (`Domains/<Name>/Code/Hud` → `<Name>.Hud`); rename folders that would shadow Unity types or other namespaces.
