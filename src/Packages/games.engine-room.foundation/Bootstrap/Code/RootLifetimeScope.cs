using System;
using Core;
using Core.Audio;
using Core.Domains;
using Core.Logging;
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

        protected override void Configure(IContainerBuilder builder)
        {
            _eventSystem.sendNavigationEvents = _gameModule != null && _gameModule.IsUiNavigationEnabled;
            builder.RegisterInstance(new ScopeRef(this, 0));
            builder.RegisterComponent(_bootCover);
            CoreInstaller.Install(builder, _coreConfig, _audioSources, GetStorageRoot());
            builder.Register<LoadingScreen>(Lifetime.Singleton).AsSelf().As<ILoadingScreen>();
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
            builder.RegisterDomain<LoadingDomain>(_loadingDescriptor);
            InstallGame(builder);

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
