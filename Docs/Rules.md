# Project Rules

Normative rules for everyone working on this project, humans and AI agents. MUST / MUST NOT follow RFC 2119. These rules override `Docs/Architecture.md` and `Docs/Coding Conventions.md` where they conflict.

## 1. No runtime object creation

Everything that exists at runtime is authored in scenes or prefabs.

Game code (Core, Shared, Bootstrap, every domain) MUST NOT:

- call `Object.Instantiate`, `Object.InstantiateAsync` or `GameObject.Instantiate`,
- call `new GameObject(...)` or `GameObject.CreatePrimitive`,
- call `AddComponent`,
- spawn prefabs at runtime in any other way (factories, pools that instantiate, VContainer `RegisterComponentOnNewGameObject` / `RegisterComponentInNewPrefab`).

Instead:

- Author every object in the scope scene, a content scene or the root prefab, and reference it through serialized fields.
- A variable number of things is a fixed authored set (for example one pickup effect per collectible, 16 authored `AudioSource`s). Size the set for the worst case in the Editor.
- Show and hide with `SetActive` / `enabled` instead of spawning and destroying.
- When a fixed set runs out, reuse an element (for example steal the oldest-started voice). Never grow it.

Editor tooling and tests MAY create objects.

## 2. No comments in code

- No `//`, no `/* */`, no XML docs (`///`).
- Only exception: the `// Arrange`, `// Act`, `// Assert` markers in tests.
- Preprocessor directives (`#if UNITY_EDITOR`, `#nullable`, `#pragma`) are allowed.

## 3. Binding documents

- `Docs/Architecture.md` and `Docs/Coding Conventions.md` are binding. Read both before changing code.
- Where they conflict, `Docs/Architecture.md` wins.

## 4. Code layout

- All scripts, asmdefs and `csc.rsp` files of a module live in `<Module>/Code/` (for example `Core/Code/Save`, `Bootstrap/Code/Editor`).
- A sub-domain is a folder inside its main domain, `Domains/<Main>/<Sub>/`, with its own `Code/` folder holding a `<Main>.<Sub>.asmref` into the main domain's assembly. It has no asmdef or `csc.rsp` of its own.
- Tests live in `<Module>/Tests/`. Shared test helpers live in `Shared/TestUtils`.
- Non-code assets (scenes, prefabs, configs, art) stay outside `Code/`.

## 5. Commits

- Make logical, focused commits: several small commits, not one large one.
- Commit only your own work. Stage explicit paths and never stage everything blindly.
- Always commit `.meta` files together with their assets.

## 6. Packages and files

- Do not touch `com.unity.pipeline` or the other default packages (`com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.collab-proxy`).
- Do not create documentation files unless the owner asks for them.
