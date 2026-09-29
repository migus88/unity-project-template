using Core.Domains;
using Core.Localization;
using Gameplay.Cameras;
using Gameplay.Collectibles;
using Gameplay.Flow;
using Gameplay.Hud;
using Gameplay.Pause;
using Gameplay.Player;
using Gameplay.Progress;
using Gameplay.Round;
using Gameplay.UserSettings;
using Settings;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Gameplay
{
    internal sealed class GameplayLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private LocalizationTable _text = null!;
        [SerializeField, Required] private GameplayConfig _config = null!;
        [SerializeField, Required] private PlayerView _playerView = null!;
        [SerializeField, Required] private GameplayCameraView _cameraView = null!;
        [SerializeField, Required] private HudView _hudView = null!;
        [SerializeField, Required] private RoundResultView _resultView = null!;
        [SerializeField, Required] private PauseDomainDescriptor _pauseDescriptor = null!;
        [SerializeField, Required] private SettingsDomainDescriptor _settingsDescriptor = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterLocalizationTable(_text);
            builder.RegisterInstance(_config);
            builder.RegisterComponent(_playerView);
            builder.RegisterComponent(_cameraView);
            builder.RegisterComponent(_hudView);
            builder.RegisterComponent(_resultView);
            builder.Register<ScoreModel>(Lifetime.Singleton);
            builder.Register<RoundService>(Lifetime.Singleton);
            builder.Register<PlayerInputState>(Lifetime.Singleton);
            builder.Register<PauseRequests>(Lifetime.Singleton);
            builder.Register<GameplayProgressService>(Lifetime.Singleton);
            builder.Register<GameplaySettingsService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<CollectiblesPresenter>().AsSelf();
            builder.RegisterEntryPoint<GameplayFlowPresenter>();
            builder.RegisterEntryPoint<PauseFlowPresenter>();
            builder.RegisterEntryPoint<HudPresenter>();
            builder.RegisterEntryPoint<PlayerInputHandler>();
            builder.RegisterEntryPoint<PlayerMovementPresenter>().AsSelf();
            builder.RegisterEntryPoint<GameplayCameraPresenter>().AsSelf();
            builder.RegisterSubDomain<PauseDomain>(_pauseDescriptor);
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
        }
    }
}
