using System;
using Core;
using Core.Analytics;
using Core.Audio;
using Core.Cheats;
using Core.Domains;
using Core.Environment;
using Core.Logging;
using Core.Platform;
using Core.Privacy;
using Core.Transitions;
using Loading;
using Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using VContainer.Unity;

namespace Bootstrap
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private CoreConfig _coreConfig = null!;
        [SerializeField] private AudioSourceSet _audioSources = null!;
        [SerializeField] private SettingsDomainDescriptor _settingsDescriptor = null!;
        [SerializeField] private LoadingDomainDescriptor _loadingDescriptor = null!;
        [SerializeField] private GameModule? _gameModule;
        [SerializeField] private EventSystem _eventSystem = null!;
        [SerializeField] private BootCoverView _bootCover = null!;
        [SerializeField] private CheatConsoleView _cheatConsole = null!;
        [SerializeField] private ActiveBuildEnvironment? _environment;

        protected override void Configure(IContainerBuilder builder)
        {
            _eventSystem.sendNavigationEvents = _gameModule != null && _gameModule.IsUiNavigationEnabled;
            builder.RegisterInstance(new ScopeRef(this, 0));
            builder.RegisterComponent(_bootCover);
            CoreInstaller.Install(builder, _coreConfig, _audioSources, GetStorageRoot());
            RegisterBuildEnvironment(builder);
            builder.Register<LoadingScreen>(Lifetime.Singleton).AsSelf().As<ILoadingScreen>();
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
            builder.RegisterDomain<LoadingDomain>(_loadingDescriptor);
            InstallGame(builder);
            RegisterUiInteractionSoundsFallback(builder);
            RegisterAnalyticsBackendFallback(builder);
            RegisterPlatformFallbacks(builder);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterCheatConsole(_cheatConsole);
            builder.RegisterCheats<ConsentCheats>();
#else
            _cheatConsole.gameObject.SetActive(false);
#endif

            Log.Info(LogTags.Boot, $"Boot mode: {BootMode.Current}.");

            switch (BootMode.Current)
            {
                case BootMode.Kind.Normal:
                    builder.RegisterEntryPoint<GameFlow>();
                    break;
#if UNITY_EDITOR
                case BootMode.Kind.DebugDomain:
                    _bootCover.Hide();
                    builder.RegisterEntryPoint<DebugDomainBoot>();
                    break;
                case BootMode.Kind.Test:
                    _bootCover.Hide();
                    break;
#endif
                default:
                    throw new InvalidOperationException($"Unsupported boot mode {BootMode.Current}.");
            }
        }

        private void InstallGame(IContainerBuilder builder)
        {
            if (_gameModule == null)
            {
                builder.Register<IMainFlow, IdleMainFlow>(Lifetime.Singleton);
                return;
            }

            _gameModule.Install(builder);
        }

        private void RegisterBuildEnvironment(IContainerBuilder builder)
        {
            if (_environment == null)
            {
                return;
            }

            if (_environment.Environment == null)
            {
                Log.Warn(LogTags.Boot, $"The build environment slot '{_environment.name}' is empty; no build environment is registered.");
                return;
            }

            builder.RegisterInstance(_environment.Environment);
        }

        private static void RegisterUiInteractionSoundsFallback(IContainerBuilder builder)
        {
            if (!builder.Exists(typeof(IUiInteractionSounds), includeInterfaceTypes: true))
            {
                builder.Register<IUiInteractionSounds, NullUiInteractionSounds>(Lifetime.Singleton);
            }
        }

        private static void RegisterAnalyticsBackendFallback(IContainerBuilder builder)
        {
            if (!builder.Exists(typeof(IAnalyticsBackend), includeInterfaceTypes: true))
            {
                builder.Register<IAnalyticsBackend, DummyAnalyticsBackend>(Lifetime.Singleton);
            }
        }

        private static void RegisterPlatformFallbacks(IContainerBuilder builder)
        {
            if (!builder.Exists(typeof(IAchievements), includeInterfaceTypes: true))
            {
                builder.Register<IAchievements, NullAchievements>(Lifetime.Singleton);
            }

            if (!builder.Exists(typeof(IPlatformPresence), includeInterfaceTypes: true))
            {
                builder.Register<IPlatformPresence, NullPlatformPresence>(Lifetime.Singleton);
            }

            if (!builder.Exists(typeof(AchievementCatalog)))
            {
                builder.RegisterInstance(AchievementCatalog.Empty);
            }
        }

        private static string GetStorageRoot()
        {
#if UNITY_EDITOR
            if (BootMode.Current == BootMode.Kind.Test)
            {
                return BootMode.TestStorageRoot;
            }
#endif
            return CoreInstaller.DefaultStorageRoot;
        }
    }
}
