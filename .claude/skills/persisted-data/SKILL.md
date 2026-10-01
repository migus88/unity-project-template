---
name: persisted-data
description: How to persist and configure data in this Unity project - per-domain save sections (ISaveStore, SaveSection<T>, slot files), domain-owned user settings sections (ISettingsService, SettingsSection<T>) versus Core settings (SettingsState, edited only by the Settings domain), versioned DTO records with nullable fields, migrations with CurrentVersion bumps, flushing and saving, newer-version and corrupted-data behaviour, and ScriptableObject configs. Use when adding or changing anything that is saved to disk (progress, unlocks, options), adding a user-facing option, renaming or reinterpreting a stored field, or adding tunable config data.
---

# Save data, settings and configs

Paths are relative to `src/Assets/_Project/`. Binding: `docs/Rules.md` (Data): changing a persisted field bumps the section's `CurrentVersion` and adds a migration step in the same change. Skeletons: `templates.md`.

## Which store?

| Data | Store | API |
|---|---|---|
| Progress, unlocks, records (per save slot) | `Saves/slot_{n}.json`, one section per domain | `ISaveStore.Read/Write(SaveSection<T>)`, `FlushAsync` (`Core/Code/Save/`) |
| An option only this domain uses (camera distance, difficulty) | `settings.json` → `sections.<key>` | `ISettingsService.Read/Write(SettingsSection<T>)`, `SaveAsync` (`Core/Code/Settings/`) |
| Volumes, language, graphics, bindings | `settings.json` → `core` | `SettingsState` via `ISettingsService.Apply` (Settings domain only) |
| Designer-tuned values | ScriptableObject in `Domains/<Name>/Configs/` | `RegisterInstance(_config)`; read-only at runtime |

## Adding a section

- [ ] DTO: `internal sealed record <Name>SaveDto(int? BestScore, ...)` in the domain's feature folder. Primitive types only, value types nullable, no Unity types, separate from the runtime model.
- [ ] Section holder: static class with `CurrentVersion`, defaults, `Key` = domain name in camelCase, `Section`, and `Migrate` (`templates.md`).
- [ ] A domain service owns all reads/writes: maps DTO → model, uses the declared default for a `null` or invalid field and logs one Warn (list the fields); handles `NotFound`/`Corrupted` from `ISaveStore.Read` by starting from defaults.
- [ ] Saves: `Write` changes memory; `FlushAsync(ct)` at meaningful points (level end, quit to menu); log a failed flush where handled. `SaveAutoFlush` also flushes on focus loss and quit.
- [ ] Settings: `Read` never fails (returns `Default` on missing/corrupted/newer); `Write` changes memory; `SaveAsync` when the UI closes. The domain edits its own settings in its own UI; the Settings domain never shows them.
- [ ] Tests: migration from every older version, missing/invalid fields, round trip with `InMemoryFileStorage` (`writing-tests`).

## Changing a section (Rules: Data)

1. Add/rename/remove/reinterpret a field → `CurrentVersion++`.
2. Add a `MigrateFromVersion<N>(JObject)` step and a `version switch` arm; steps chain one version at a time and return `Corrupted` when data is unusable.
3. Test migrating a stored JSON of the old version.
4. Never write back data from a newer game version: the store already keeps it untouched; do not work around that.
Envelope changes (`formatVersion`, `sections`, `core`) are Core changes: bump `CurrentFormatVersion` in `Core/Code/Save/SaveStore.cs` or `Core/Code/Settings/SettingsService.cs` and keep reading the old format.

## Core settings

A new Core setting touches `Core/Code/Settings/SettingsState.cs`, `CoreSettingsDto.cs`, validation and apply in `SettingsService.cs`, defaults in `SettingsDefaults.cs`/`CoreConfig`, and its tests in `Core/Tests/Settings/`. Prefer a domain section unless every game needs it.

## Pitfalls

- `JsonUtility`, `System.IO`, `JsonConvert` in game code: forbidden; go through `ISaveStore`/`ISettingsService` (and `IJsonSerializer`/`IFileStorage` in Core only).
- Non-nullable value fields turn a missing value into a silent 0.
- Records and `readonly` fields do not serialize in Unity: never use them for ScriptableObject/MonoBehaviour fields.
- Deleting a domain leaves an orphaned section; that is intended.
