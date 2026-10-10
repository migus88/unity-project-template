---
name: domain-feature
description: How to add or change logic inside a domain in this Unity project - presenters and flow presenters (VContainer entry points), domain services and models, DomainCompletion, registration in the domain's LifetimeScope, lifecycle interfaces (IStartable, IAsyncStartable, ITickable, IDisposable), UniTask async and cancellation, R3 subscriptions and ReactiveProperty state, input handlers over the generated GameInput callbacks, input map stack and MLock input locks, pausing time, timers, audio cues, analytics events and ScriptableObject configs. Use when implementing gameplay or feature behaviour, wiring a new presenter/service/model, handling input, or reviewing such code.
---

# Adding logic to a domain

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/` (Core, Shared, Bootstrap, Loading, Settings) or the game's `src/Assets/_Project/` (game module, game domains). Binding rules: `docs/Rules.md` (DI and lifecycle, Presentation, Async and errors) and `docs/CodingConventions.md`. Views and text: `ui-views`. Skeletons: `patterns.md`.

## Pick the role

| Need | Type | Register in `<Name>LifetimeScope.ConfigureDomain` |
|---|---|---|
| Drives views, reacts to input/state | `...Presenter` (`IStartable` / `IAsyncStartable` + `IDisposable`) | `builder.RegisterEntryPoint<T>()` (`.AsSelf()` if another class calls it) |
| Owns the domain's flow and its end | one flow presenter injecting `DomainCompletion<TResult>` | `RegisterEntryPoint<T>()` |
| Rules, calculations, persistence access | `...Service` (plain class) | `builder.Register<T>(Lifetime.Singleton)`; interface only for a real seam |
| Shared mutable state | `...Model` with private `ReactiveProperty<T>` exposed read-only | `Register<T>(Lifetime.Singleton)` |
| One-off request between presenters | small class with private `Subject<Unit>` + `Request()` | `Register<T>(Lifetime.Singleton)` |
| Raw input → intent | `...InputHandler` implementing `GameInput.I<Map>Actions` (+ `ILockable<InputLockTag>`) | `RegisterEntryPoint<T>()` |
| Tunable data | ScriptableObject config in `Configs/` | `[SerializeField]` on the scope + `builder.RegisterInstance(_config)` |

Available from the parent scopes: every Core service (`Core/Code/CoreInstaller.cs`), the domain's `Args` record, its `<Name>Content`, `DomainCompletion<TResult>`, `DomainSceneSet`, `ScopeRef`. A sub-domain also sees its main domain's registrations. Use `goToImplementation`/`findReferences` (LSP) on a Core interface to see how it is used.

## Checklist

- [ ] Class is `internal sealed`, in a feature folder (`Code/<Feature>/`), explicit constructor assigning `private readonly` fields.
- [ ] Registered in the scope; nothing resolves it manually; no `[Inject]`.
- [ ] Subscriptions in a `DisposableBag` (`.AddTo(ref _subscriptions)`), disposed in `Dispose()`; input map handles (`_input.Push(...)`) and locks disposed on every path.
- [ ] Async: `UniTask`, `CancellationToken ct` last with no default; root token from `IAsyncStartable.StartAsync`, from `SubscribeAwait`, or a private `CancellationTokenSource` cancelled in `Dispose`.
- [ ] Expected failures → OneOf unions, consumed with `Match`/`Switch`/`TryPickT0`; bugs throw; log with `Log` + the assembly's `LogTags` where handled.
- [ ] Exactly one place calls `DomainCompletion.Complete`; guard with `IsCompleted` where inputs can race.
- [ ] Per-frame work only in `ITickable`/`IFixedTickable`/`ILateTickable` that applies continuous values (no LINQ, no polling input, no flow).
- [ ] Timers that must stay aligned use `ITimerService` (`Game` stops while paused, `Real` does not); pause with `using var pause = _time.Pause();`-style handles from `ITimeService`.
- [ ] Sounds are `AudioCue` assets referenced from the domain's config, played through `IAudioService`. Looping SFX: `IAudioService.PlayLoop(cue)` (cue `Loop` on); the owner keeps the returned `AudioLoop`, stops it when the sound should end and disposes it in `Dispose`.
- [ ] Nothing spawned: variable counts are fixed authored sets; one presenter drives a collection of item views.
- [ ] Tests for services, models and non-trivial presenters (`writing-tests`).
- [ ] Debug cheats for the domain: skill `cheat-console`.
- [ ] Analytics: player-facing decisions and screens are tracked with `IAnalytics.Track(new FooEvent(...))` from the presenter or service that owns the decision. The event is a `readonly record struct` implementing `IAnalyticsEvent` in the feature's `Analytics/` folder, with a constant, unique snake_case `Name` and a `Write` that adds primitives and content ids only (no free text, paths or personal data). Tests assert one representative emission with `TestUtils.FakeAnalytics`. Examples: `Core/Code/Cheats/CheatUsedEvent.cs`, `Core/Code/Settings/SettingsChangedEvent.cs`.

## Input

- One handler per (domain, action map). Implement every action of the generated interface (unused ones get empty bodies). `AddCallbacks(this)` in `Start`, `RemoveCallbacks(this)` in `Dispose`.
- Discrete actions act on `context.performed`; continuous actions cache the value (`performed` → value, `canceled` → zero) into a model that a tickable presenter applies.
- Maps: the flow presenter pushes its maps (`_input.Push(InputMaps.Player | InputMaps.Ui)`) and disposes the handle; overlays push `InputMaps.Ui` on top.
- Locks: handlers implement `ILockable<InputLockTag>` (`LockTags`, `HandleLocking` zeroes cached values, `HandleUnlocking` re-reads them; `Subscribe(this)`/`Unsubscribe(this)`). Lockers: `using var inputLock = _locks.Lock(InputLockTag.Movement);`. New tag categories go in `Core/Code/Input/InputLockTag.cs`. MLock is a git-URL package (`com.migsweb.mlock` in `src/Packages/manifest.json`, pinned to a tag); never edit it in `Library/PackageCache`: changes go upstream to github.com/migus88/MLock, then bump the tag.
- New actions: edit `Core/Input/GameInput.inputactions` in the Editor; `Core/Code/Input/GameInput.cs` is generated by `Core/Code/Editor/Input/GameInputGenerator.cs` on import (or menu `Tools/Input/Generate GameInput`; the importer's own wrapper generation stays off), never hand-edited.

## Verify

Refresh, recompile, empty error console, run the domain's EditMode tests (`docs/UnityCli.md`). For wiring, press Play in the domain's scope scene and read the console.

## Pitfalls

- A view driven by two presenters: route through the owning presenter instead (`PlaceAt`, `Follow`-style methods).
- Observables used for flow between services or long operator chains: use `async`/`await` and results.
- `async void`, C# `event`s, public `Subject`s, `FindObjectOfType`, `Debug.Log`: all forbidden.
- Inside `Core.*` namespaces `Time` means `Core.Time`; write `UnityEngine.Time`.
