# Project Rules

Normative rules for everyone working on this project, humans and AI agents. MUST / MUST NOT follow RFC 2119. These rules override `Docs/Architecture.md` and `Docs/Coding Conventions.md` where they conflict.

## 1. No runtime object creation

Everything that exists at runtime is authored in scenes or prefabs.

Game code (Core, Shared, Bootstrap, every domain) MUST NOT:

- call `Object.Instantiate`, `Object.InstantiateAsync` or `GameObject.Instantiate`,
- call `new GameObject(...)` or `GameObject.CreatePrimitive`,
- call `AddComponent`,
- spawn prefabs at runtime in any other way (factories, pools that instantiate, VContainer `RegisterComponentOnNewGameObject` / `RegisterComponentInNewPrefab`),
- use UI widgets that instantiate objects at runtime (for example `TMP_Dropdown`, which builds its option list and a blocker each time it opens). Use an authored widget instead, such as `Shared.UI.SelectorView` (previous/next buttons and a label).

Instead:

- Author every object in the scope scene, a content scene or the root prefab, and reference it through serialized fields.
- A variable number of things is a fixed authored set (for example one pickup effect per collectible, 16 authored `AudioSource`s). Size the set for the worst case in the Editor.
- Show and hide with `SetActive` / `enabled` instead of spawning and destroying.
- When a fixed set runs out, reuse an element (for example steal the oldest-started voice). Never grow it.

Allowed exceptions, and only these:

- VContainer instantiating the authored root prefab (`RootLifetimeScope`) from `VContainerSettings`.
- The generated `GameInput` constructor, which builds an in-memory `InputActionAsset` (no GameObject).
- Editor tooling and tests MAY create objects.

## 2. No comments in code

- No `//`, no `/* */`, no XML docs (`///`).
- Exceptions: the `// Arrange`, `// Act`, `// Assert` markers in tests, and `Core/Code/Input/GameInput.cs`, which the Input System generates (its comments are tool-owned). First-party generators emit no comments.
- Preprocessor directives (`#if UNITY_EDITOR`, `#nullable`, `#pragma`) are allowed.

## 3. Binding documents

- `Docs/Architecture.md` and `Docs/Coding Conventions.md` are binding. Read both before changing code.
- Where they conflict, `Docs/Architecture.md` wins.

## 4. Code layout

- Runtime and Editor scripts, asmdefs and `csc.rsp` files of a module live in `<Module>/Code/` (for example `Core/Code/Save`, `Bootstrap/Code/Editor`).
- A sub-domain is a folder inside its main domain, `Domains/<Main>/<Sub>/`, with its own `Code/` folder holding a `<Main>.<Sub>.asmref` into the main domain's assembly. It has no asmdef or `csc.rsp` of its own.
- Test assemblies (asmdef, `csc.rsp` and test scripts) live in `<Module>/Tests/`.
- `Shared/TestUtils` is the shared test-helper assembly. Its asmdef, `csc.rsp` and scripts sit at its root, without a `Code/` folder.
- Non-code assets (scenes, prefabs, configs, art) stay outside `Code/`.

## 5. Commits

- Make logical, focused commits: several small commits, not one large one.
- Commit only your own work. Stage explicit paths and never stage everything blindly.
- Always commit `.meta` files together with their assets.

## 6. Packages and files

- Do not touch `com.unity.pipeline` or the other default packages (`com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.collab-proxy`).
- Do not create documentation files unless the owner asks for them.

## 7. Persisted data

- Adding, renaming or removing a field of a persisted save or settings DTO, or changing what a stored value means (units, range, encoding), MUST bump that section's `CurrentVersion` and add a migration step from the previous version in the same change.
- A change to the file envelope itself (`formatVersion`, `sections`, `core`) MUST bump the file's `CurrentFormatVersion` and keep reading the previous format.
