---
name: cheat-console
description: How to add debug cheats to the foundation's in-game cheat console in this Unity project - ICheat classes and ICheatProvider groups (CheatCommand), declared parameters (CheatParameter.Int/Float/Bool/Enum/Text/Choice, Optional, value providers for autocomplete), CheatArguments, registering with RegisterCheat/RegisterCheats in a domain scope so cheats live and die with it, reply conventions, the UNITY_EDITOR || DEVELOPMENT_BUILD guard, tests, and driving the console from an agent through CheatConsoleView.Submit and LastReply. Use when adding, changing or testing a cheat or debug command, or when an agent needs to run a cheat in Play mode.
---

# Cheat console

Paths are relative to the foundation package `src/Packages/games.engine-room.foundation/`.

The console is app-lifetime infrastructure in Core (`Core/Code/Cheats/`), authored as the `CheatConsole` child of `Bootstrap/Prefabs/RootLifetimeScope.prefab` and registered by `Bootstrap/Code/RootLifetimeScope.cs` (`RegisterCheatConsole`). Backquote toggles it (top half of the screen), Tab completes names and values, Up/Down recall history, Escape closes it; `help [cheat]` and `clear` are built in. While open it pushes `InputMaps.None` and locks every `InputLockTag`. Everything except the passive `Core/Code/Cheats/CheatConsoleView.cs` compiles only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.

## Add a cheat

- [ ] One command: a class implementing `ICheat` (`Name` is one lower-case word, unique among the cheats alive at the same time; `Description` is one short sentence; `Parameters`; `ExecuteAsync` returns the reply). Template: `Core/Code/Cheats/ClearConsoleCheat.cs`.
- [ ] Several commands that share services: one `ICheatProvider` whose `Cheats` list holds `CheatCommand`s (name, description, parameters, a sync `Func<CheatArguments, string>` or async delegate).
- [ ] Declare arguments, never parse text: `CheatParameter.Int`, `Float` (invariant culture), `Bool` (on/off, yes/no, true/false, 1/0), `Enum<T>`, `Text(name, suggestions)` (free text; suggestions only feed Tab), `Choice(name, values)` (must resolve to one value: case, spaces and underscores ignored, unique prefixes accepted). `.Optional()` parameters come last. A last `Text`/`Choice` takes the rest of the line, so names with spaces need no quotes.
- [ ] Read values with `CheatArguments.GetInt/GetFloat/GetBool/GetText/GetEnum<T>` (overloads with a default for optional ones, or `HasValue`). Reading a missing required argument throws: it is a bug.
- [ ] To look up domain objects by name use `CheatValues.Match(items, nameOf, input)`, the same rule as `Choice`.
- [ ] Register in the scope that owns the services, inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`: `builder.RegisterCheat<T>()` or `builder.RegisterCheats<TProvider>()` (`Core/Code/Cheats/CheatRegistrationExtensions.cs`). The cheat is added when the scope starts and removed when it is disposed; no static registry. Guard the cheat files with the same symbol.
- [ ] Tests: build the cheat by hand with fakes; parse lines with `CheatArgumentParser.Parse(cheat, CheatLine.Parse("name args"))` and assert the reply. Package examples: `Core/Tests/Cheats/`.

## Replies

- Short, factual, past tense when something changed ("Added 50 gold."); explain refusals ("Only during the player's turn."). Return them; never throw for expected refusals. An exception escaping a cheat is logged and answered `Error: <message>` by the console.
- Tabs split a reply into aligned columns (the console turns them into TMP `<pos>` tags); everything else is shown as plain text, so `<` and `>` need no escaping.
- Every reply is logged with the `Cheats` tag.

## Agents

In Play mode, from `unity command eval` (or `run_script`): `UnityEngine.Object.FindAnyObjectByType<Core.Cheats.CheatConsoleView>().Submit("help")`, then read `.LastReply`. `Submit` works with the console closed; synchronous cheats reply before `Submit` returns, async ones after the frames they await.

## Pitfalls

- Two live scopes registering the same name throw at registration (`InvalidOperationException`).
- A cheat exists only while its scope lives: `help` lists what is registered right now.
- Release builds keep only the hidden `CheatConsoleView`; code outside the guard must not reference other `Core.Cheats` types.
