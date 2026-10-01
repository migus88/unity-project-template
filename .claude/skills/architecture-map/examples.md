# Worked examples (sample content, may have been removed)

The template ships a small sample game. Any of these files may be gone after the sample is removed; the checker (`.claude/skills/check-skills.sh`) only reports missing paths in this file as notes. When the sample is removed, the skills still work: they carry their own skeletons and cite Core/Shared/Bootstrap files. Paths are relative to `src/Assets/_Project/`.

| Pattern | Example |
|---|---|
| Smallest main domain (menu, R3 outputs, launching a leaf overlay) | `Domains/MainMenu/Code/MainMenuPresenter.cs`, `Domains/MainMenu/Code/MainMenuView.cs` |
| Leaf domain reused by two launchers, editing Core settings, authored selector | `Domains/Settings/Code/SettingsPresenter.cs`, `Domains/Settings/Code/SettingsLifetimeScope.cs` |
| Long-lived parallel domain, Core contract implemented by a domain, `IInitializable` | `Domains/Loading/Code/LoadingScreen.cs`, `Domains/Loading/Code/LoadingScreenPresenter.cs` |
| Presenter tested through a view interface, hand-written fake for an internal interface | `Domains/Loading/Tests/LoadingScreenTests.cs`, `Domains/Loading/Code/ILoadingScreenView.cs` |
| Flow presenter, content scene load and root-view lookup | `Domains/Gameplay/Code/Flow/GameplayFlowPresenter.cs`, `Domains/Gameplay/Code/Room/RoomView.cs` |
| Scope with many registrations, sub-domain and leaf registration | `Domains/Gameplay/Code/GameplayLifetimeScope.cs` |
| Content root with content scenes | `Domains/Gameplay/Code/GameplayContent.cs` |
| Input handler with MLock locking, continuous input applied by a tickable | `Domains/Gameplay/Code/Player/PlayerInputHandler.cs`, `Domains/Gameplay/Code/Player/PlayerMovementPresenter.cs` |
| Pause loop: time pause, lock, sub-domain returning a case to its parent | `Domains/Gameplay/Code/Flow/PauseFlowPresenter.cs`, `Domains/Gameplay/Pause/Code/PausePresenter.cs` |
| Sub-domain folder with asmref | `Domains/Gameplay/Pause/Code/Gameplay.Pause.asmref` |
| One presenter driving a fixed authored set of item views | `Domains/Gameplay/Code/Collectibles/CollectiblesPresenter.cs` |
| Model with `ReactiveProperty`, request channel | `Domains/Gameplay/Code/Round/ScoreModel.cs`, `Domains/Gameplay/Code/Flow/PauseRequests.cs` |
| Timer countdown on the game clock | `Domains/Gameplay/Code/Round/RoundService.cs` |
| Save section with a v1 → v2 migration, nullable DTO mapping | `Domains/Gameplay/Code/Progress/GameplaySave.cs`, `Domains/Gameplay/Code/Progress/GameplayProgressService.cs` |
| Domain-owned settings section and its service | `Domains/Gameplay/Code/UserSettings/GameplaySettings.cs`, `Domains/Gameplay/Code/UserSettings/GameplaySettingsService.cs` |
| Config ScriptableObject with audio cues | `Domains/Gameplay/Code/GameplayConfig.cs` |
| Domain test assembly | `Domains/Gameplay/Tests/Gameplay.Tests.asmdef`, `Domains/Gameplay/Tests/Progress/GameplaySaveTests.cs` |
| Game module and main flow | `Sample/Code/SampleGameModule.cs`, `Sample/Code/SampleMainFlow.cs` |
