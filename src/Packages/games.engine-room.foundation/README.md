# Engine Room Foundation

The game-agnostic foundation of the [Unity project template](https://github.com/migus88/unity-project-template): boot and composition root, VContainer scopes, the domain lifecycle, save and settings, input, audio, localization, content directories, a loading screen and a settings screen. A game plugs into it as a **game module** (a `GameModule` asset plus an `IMainFlow`) and never edits the package.

| Folder | Assemblies | What it is |
|---|---|---|
| `Core/` | `Core`, `Core.Editor`, `Core.Tests` | App-lifetime infrastructure and editor tooling |
| `Shared/UI/`, `Shared/TestUtils/` | `Shared.UI`, `TestUtils` | Reusable UI widgets and prefabs, test helpers |
| `Bootstrap/` | `Bootstrap`, `Bootstrap.Editor`, `Bootstrap.Tests`, `Bootstrap.PlayModeTests` | Root prefab, default `VContainerSettings`, fallback `Bootstrap.unity`, boot scene tools, play-from-any-scene |
| `Domains/Loading/`, `Domains/Settings/` | `Loading`, `Settings` (+ tests) | Leaf domains: loading screen and settings screen |

The architecture, rules and coding conventions live in the template repo's `docs/` and `.claude/skills/`.

## Requirements

Unity 6000.6 or newer. Declared dependencies (UniTask, VContainer, NuGetForUnity, Input System, uGUI, Cinemachine, Newtonsoft Json, Test Framework) resolve automatically once the OpenUPM registry is present. The rest cannot be declared in `package.json` and must be in the game project:

1. **OpenUPM scoped registry** in `Packages/manifest.json`:

   ```json
   "scopedRegistries": [
     {
       "name": "package.openupm.com",
       "url": "https://package.openupm.com",
       "scopes": ["com.cysharp.unitask", "com.github-glitchenzo.nugetforunity", "jp.hadashikick.vcontainer"]
     }
   ]
   ```

2. **Git packages** in `Packages/manifest.json` (`git` must be on `PATH`):

   ```json
   "com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.1",
   "com.migsweb.mlock": "https://github.com/migus88/MLock.git?path=src/mlock-unity-project/Packages/MLock#2.1.0",
   "games.engine-room.foundation": "https://github.com/migus88/unity-project-template.git?path=src/Packages/games.engine-room.foundation#1.0.0"
   ```

   Add `"testables": ["games.engine-room.foundation"]` to run the package's tests in the game.

3. **NuGet packages** through NuGetForUnity (`packages.config`): `OneOf` 3.0.271, `OneOf.SourceGenerator` 3.0.271, `R3` 1.3.1, and for tests `AwesomeAssertions` 9.6.0 and `NSubstitute` 6.2.0 (with `autoReferenced="false"`), plus their dependencies. The template's `src/Packages/nuget-packages/packages.config` is the reference list.

4. **TextMesh Pro essentials** imported (Window > TextMeshPro > Import TMP Essential Resources).

5. **Project settings**: the game's empty boot scene `Assets/_Project/<Game>/Scenes/Boot.unity` as the first scene in Build Settings, and the game's `VContainerSettings` asset in Preloaded Assets. **Tools > Foundation > Create Game Module** sets up both. The package's `Bootstrap/Scenes/Bootstrap.unity` is only the fallback (Play in the Editor boots through it when Build Settings list no enabled scene); a game consuming the package from git cannot open it. The boot scene stays empty: everything app-lifetime is authored in the root prefab, and a `Bootstrap.Tests` EditMode test and the build warn otherwise.

6. **IDE / csharp-ls**: Preferences > External Tools > Generate .csproj files for: enable **Git packages**, so the package's code is in the solution.

Known limits: the `Language` enum lives in Core (adding a language is a package change), and the Loading and Settings scenes are read-only in a game that consumes the package from git (restyle them by overriding descriptors in the game's root prefab variant, or embed the package).

## Cheat console

Editor and development builds only (`UNITY_EDITOR || DEVELOPMENT_BUILD`); release builds keep a hidden, inert `CheatConsoleView` on the root prefab.

- **Use it:** backquote opens and closes it over the top half of the screen. A list under the input shows the matching cheat names and argument values while typing: Up/Down move through it, Tab or Enter accept the highlighted one, Escape hides it. Without a highlight Tab completes; without a list Up/Down recall earlier lines and Escape closes the console. `help` lists every registered cheat, `help <cheat>` describes one and its values, `clear` empties the output. While it is open, game input is off (`InputMaps.None` and every `InputLockTag` locked).
- **Add a cheat:** a class implementing `Core.Cheats.ICheat` (or an `ICheatProvider` of `CheatCommand`s), registered in the scope that owns its services, so it exists only while that scope lives:

  ```csharp
  #if UNITY_EDITOR || DEVELOPMENT_BUILD
  builder.RegisterCheat<GoldCheat>();
  #endif
  ```

  Arguments are declared with `CheatParameter.Int/Float/Bool/Enum<T>/Text/Choice` (and `.Optional()`) and read from `CheatArguments`; the console parses, validates and autocompletes them. The project skill `cheat-console` has the checklist.
- **Agents:** in Play mode, `unity command eval --code 'var c = UnityEngine.Object.FindAnyObjectByType<Core.Cheats.CheatConsoleView>(); c.Submit("help"); return c.LastReply;'`. Replies are also logged with the `Cheats` tag.

## Workflows

### Update a game to a new version

1. Read this `CHANGELOG.md` for the versions in between (a major version means breaking changes).
2. Change the tag in the game's `Packages/manifest.json`: `...games.engine-room.foundation#X.Y.Z`.
3. Open Unity, let it resolve and compile, apply the CHANGELOG's migration notes, run the EditMode and PlayMode tests.

**Game-owned boot scene (1.1.0)**: a game set up on 1.0.0 lists the package's read-only `Bootstrap.unity` first in Build Settings. Run **Tools > Foundation > Create Boot Scene** (without a dialog: `Bootstrap.Editor.GameBootScene.CreateForActiveModule`): it creates the empty `Scenes/Boot.unity` next to the active game module's `VContainerSettings` and replaces the package scene at index 0 in Build Settings. It is safe to run again.

### Change the package locally in a game

Copy `Library/PackageCache/games.engine-room.foundation@<hash>` to `Packages/games.engine-room.foundation` in the game. An embedded package wins over the manifest entry, so Unity uses the copy and it is editable. To go back to the git version, use **Tools > Foundation > Use Package From Git** (enabled only while the package is embedded and its manifest entry is a git URL): it warns when the embedded `package.json` version differs from the manifest's revision, deletes the embedded folder and resolves packages. Without a dialog (agents, `-executeMethod`): `Core.Editor.Packages.GitPackageSwitch.UseFromGit`. Local edits are lost unless they are contributed back.

### Contribute a change back

1. Clone the template repo.
2. In the game's `Packages/manifest.json`, point the package at the clone temporarily (do not commit this): `"games.engine-room.foundation": "file:<clone>/src/Packages/games.engine-room.foundation"`.
3. Edit and test in the game, then open the template project (`src/`), run its tests, add the change under `## [Unreleased]` in `CHANGELOG.md`, and open a pull request.
4. After the release, set the game's manifest back to the git URL with the new tag.

### Release a version (template maintainers)

1. Pick the version (semver): **major** for breaking public API, persisted data compatibility, removing or recreating objects in the base `RootLifetimeScope.prefab`, or renaming assemblies; **minor** for features; **patch** for fixes.
2. Set `version` in `package.json` and the tag in the template's `src/Packages/manifest.json` entry to the same value.
3. Move the `Unreleased` entries in `CHANGELOG.md` under `## [X.Y.Z] - YYYY-MM-DD`.
4. Commit, `git tag X.Y.Z`, `git push origin main X.Y.Z`.

In the template project the embedded folder overrides the manifest's git entry, so the template always runs the working copy.
