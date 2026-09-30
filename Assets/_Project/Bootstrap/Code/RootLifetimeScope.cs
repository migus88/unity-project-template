using System;
using Core;
using Core.Audio;
using Core.Domains;
using Core.Logging;
using Core.Transitions;
using Gameplay;
using Loading;
using MainMenu;
using Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Bootstrap
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private CoreConfig _coreConfig = null!;
        [SerializeField] private AudioSourceSet _audioSources = null!;
        [SerializeField] private MainMenuDomainDescriptor _mainMenuDescriptor = null!;
        [SerializeField] private GameplayDomainDescriptor _gameplayDescriptor = null!;
        [SerializeField] private SettingsDomainDescriptor _settingsDescriptor = null!;
        [SerializeField] private LoadingDomainDescriptor _loadingDescriptor = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new ScopeRef(this, 0));
            CoreInstaller.Install(builder, _coreConfig, _audioSources, GetStorageRoot());
            builder.Register<LoadingScreen>(Lifetime.Singleton).AsSelf().As<ILoadingScreen>();
            builder.RegisterDomain<MainMenuDomain>(_mainMenuDescriptor);
            builder.RegisterDomain<GameplayDomain>(_gameplayDescriptor);
            builder.RegisterDomain<SettingsDomain>(_settingsDescriptor);
            builder.RegisterDomain<LoadingDomain>(_loadingDescriptor);

            Log.Info(LogTags.Boot, $"Boot mode: {BootMode.Current}.");

            switch (BootMode.Current)
            {
                case BootMode.Kind.Normal:
                    builder.RegisterEntryPoint<GameFlow>();
                    break;
#if UNITY_EDITOR
                case BootMode.Kind.DebugDomain:
                    builder.RegisterEntryPoint<DebugDomainBoot>();
                    break;
                case BootMode.Kind.Test:
                    break;
#endif
                default:
                    throw new InvalidOperationException($"Unsupported boot mode {BootMode.Current}.");
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
