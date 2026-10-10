---
name: core-service
description: How to add or change app-lifetime infrastructure in Core (src/Packages/games.engine-room.foundation/Core, the foundation package) of this Unity project - a new Core service folder with interface and implementation, registration in CoreInstaller, edge adapters that wrap throwing Unity/IO/JSON APIs into OneOf unions, seams over static Unity APIs for testability, shared result types in Core.Results, LogTags, analytics (IAnalytics, the IAnalyticsBackend seam, ANALYTICS_ENABLED), Core.Editor tooling, and the optional cross-domain contract escape hatch (Core interface + Null default, domain implementation registered by Bootstrap, like ILoadingScreen). Use when something is needed by several domains for the whole app lifetime, when wrapping a Unity or third-party API, or when changing an existing Core service.
---

# Core services

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/` (Core, Shared, Bootstrap, Loading, Settings) or the game's `src/Assets/_Project/` (game module, game domains). Rules: `docs/Rules.md` (Boundaries, Async and errors) and `docs/CodingConventions.md`. Core references nothing first-party: no domain, Shared or Bootstrap types, ever. Core is part of the foundation package: a change is a package change (add it under `Unreleased` in `src/Packages/games.engine-room.foundation/CHANGELOG.md`; breaking public API or persisted data means a major version).

## Is it Core?

- Needed for the whole app lifetime or by several domains, with no UI of its own → Core.
- Has its own lifetime, UI or scenes → a domain (`new-domain`), possibly a long-lived one running in parallel.
- A Core contract that a domain implements (optional feature the game must survive without) → escape hatch below.

## Checklist

- [ ] Folder `Core/Code/<Service>/` → namespace `Core.<Service>`. The folder name must not shadow a Unity type or namespace (`Core.Time` already does: write `UnityEngine.Time` inside Core).
- [ ] `I<Service>Service` (public) + `<Service>Service` (public sealed, explicit constructor). Interfaces only for real seams (Core contracts almost always are).
- [ ] Register in `Core/Code/CoreInstaller.cs` inside a small `Install<Service>` method: `builder.Register<IFoo, Foo>(Lifetime.Singleton)`, or `RegisterEntryPoint<Foo>().As<IFoo>()` when it ticks or starts. Scene objects and paths come in as `Install` parameters, asset references through `Core/Code/CoreConfig.cs`; never loose scene objects in the container.
- [ ] `IObjectResolver` only inside registration lambdas/build callbacks.
- [ ] Expected failures return unions: inline `OneOf<A, B>` for Core returns built from shared cases, named `[GenerateOneOf]` unions for richer public results. Reuse `Core/Code/Results/NotFound.cs`, `Corrupted.cs`, `Error.cs`; alias `using Success = OneOf.Types.Success;`.
- [ ] Edge adapter: the only place with `try/catch`, catching the specific exceptions of the wrapped API and returning unions; never catches `OperationCanceledException`. Template: `Core/Code/Storage/FileStorage.cs` (`IFileStorage`), `Core/Code/Storage/JsonSerializer.cs`, `Core/Code/Input/InputBindingOverrides.cs`.
- [ ] Byte files go through `IFileStorage.ReadBytesAsync`/`WriteBytesAsync`, compression through `Core.Compression.Deflate` (`Decompress` takes a size cap for untrusted input); never `MemoryStream`/`DeflateStream` outside Core.
- [ ] Seam over a static Unity API so logic around it is unit-tested: template `Core/Code/Settings/IGraphicsDevice.cs` + `UnityGraphicsDevice.cs`, `Core/Code/IApplicationService.cs`, `Core/Code/Time/IRealClock.cs`.
- [ ] Logging: tags in `Core/Code/LogTags.cs`; log where handled, not where created.
- [ ] Tests in `Core/Tests/<Service>/` (`writing-tests`). `Core/Code/AssemblyInfo.cs` already exposes internals to `Core.Tests`.
- [ ] Editor-only tooling goes in `Core/Code/Editor/` (asmdef `Core.Editor`, Editor platform only); its tags in `Core/Code/Editor/LogTags.cs`.

## Escape hatch: Core contract implemented by a domain

Use rarely. Pattern: `Core/Code/Transitions/ILoadingScreen.cs` + `Core/Code/Transitions/NullLoadingScreen.cs`. Variant implemented by the game module instead of a domain: `Core/Code/Audio/IUiInteractionSounds.cs` + `Core/Code/Audio/NullUiInteractionSounds.cs` (the game module registers its implementation; `Bootstrap/Code/RootLifetimeScope.cs` registers the Null default only when nothing else did, via `builder.Exists(typeof(IFoo), includeInterfaceTypes: true)` after installing the game).

1. Core declares `IFoo` and a `NullFoo` no-op default. Core registers neither.
2. The domain implements `Foo : IFoo` as a public class (the one exception to domain visibility).
3. Bootstrap registers exactly one: the domain's implementation in the root (`Bootstrap/Code/RootLifetimeScope.cs` or the game module), or `builder.Register<IFoo, NullFoo>(Lifetime.Singleton)` when the domain is absent.
4. Consumers inject `IFoo` and never check for presence.

Same shape for analytics: `Core/Code/Analytics/IAnalyticsBackend.cs` is the seam a real server client implements (registered by the game module); `RootLifetimeScope` falls back to `Core/Code/Analytics/DummyAnalyticsBackend.cs` (logs `[Analytics]` lines in Editor and development builds). Code tracks through `IAnalytics` (`Core/Code/Analytics/IAnalytics.cs`); `Core/Code/Analytics/AnalyticsGate.cs` swaps in `NullAnalytics` unless the scripting define `ANALYTICS_ENABLED` is set.

## Changing an existing Core service

- Find every consumer first: LSP `findReferences` on the interface member (or Grep across `Domains/`, `Bootstrap/`, `Shared/`).
- Adding a case to a public union intentionally breaks every `Match`/`Switch`: fix them all.
- Persisted formats (save/settings envelopes, DTOs): `persisted-data`.
- If the change alters a pattern that a skill describes, update the skill in the same change (see "Skills maintenance" in `docs/Rules.md`).

## Verify

Refresh, recompile, empty error console, run `Core.Tests` and the PlayMode smoke test (`docs/UnityCli.md`).
