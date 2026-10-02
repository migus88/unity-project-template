# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `GameModule.IsUiNavigationEnabled` (off by default): turns on keyboard/gamepad UI navigation on the root EventSystem. `RootLifetimeScope` applies it at boot.

### Changed

- The root prefab's EventSystem no longer sends navigation events (Move/Submit/Cancel) by default, so Enter, Space or gamepad South no longer re-click the last clicked uGUI button. Games that use keyboard or gamepad UI navigation tick `IsUiNavigationEnabled` on their game module asset.

## [1.0.0] - 2026-10-01

### Added

- First release as a package, extracted from the Unity project template: Core (domain runner, scopes, save store, settings, input, audio, localization, content directories, logging), Shared (UI widgets, TestUtils), Bootstrap (root prefab, default `VContainerSettings`, `Bootstrap.unity`, play-from-any-scene), and the Loading and Settings domains, with their tests.
- Editor tools: `Tools/Foundation/Create Game Module`, `Tools/Foundation/Use Package From Git`, `Tools/Input/Generate GameInput`, `Tools/Localization/Generate Text Keys`, `Build/Content Directories`.
