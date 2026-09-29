using System.IO;
using Core.Audio;
using Core.Content;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Core.Save;
using Core.Settings;
using Core.Storage;
using Core.Time;
using Migs.MLock;
using Migs.MLock.Debugging;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Core
{
    public static class CoreInstaller
    {
        public static string DefaultStorageRoot
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.persistentDataPath, "Editor");
#else
                return Application.persistentDataPath;
#endif
            }
        }

        public static void Install(IContainerBuilder builder, CoreConfig config, AudioSourceSet audioSources, string storageRoot)
        {
            builder.RegisterEntryPointFailureHandler();
            builder.RegisterInstance(config);
            InstallApplication(builder);
            InstallStorage(builder, storageRoot);
            InstallSave(builder);
            InstallContent(builder);
            InstallInput(builder);
            InstallTime(builder);
            InstallAudio(builder, config, audioSources);
            InstallLocalization(builder, config);
            InstallSettings(builder, config);
            InstallDomains(builder);
            builder.Register<CoreStartup>(Lifetime.Singleton);
        }

        private static void InstallApplication(IContainerBuilder builder)
        {
            builder.Register<IApplicationService, ApplicationService>(Lifetime.Singleton);
        }

        private static void InstallStorage(IContainerBuilder builder, string storageRoot)
        {
            builder.Register<IFileStorage>(_ => new FileStorage(storageRoot), Lifetime.Singleton);
            builder.Register<IJsonSerializer, JsonSerializer>(Lifetime.Singleton);
        }

        private static void InstallSave(IContainerBuilder builder)
        {
            builder.Register<ISaveStore, SaveStore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SaveAutoFlush>();
        }

        private static void InstallContent(IContainerBuilder builder)
        {
            builder.Register<IContentDirectoryRegistry>(_ => CreateContentDirectoryRegistry(), Lifetime.Singleton);
            builder.Register<IContentLoader, ContentLoader>(Lifetime.Singleton);
            builder.Register<ISceneLoader, SceneLoader>(Lifetime.Singleton);
            builder.RegisterBuildCallback(resolver => resolver.Resolve<IContentDirectoryRegistry>());
        }

        private static void InstallInput(IContainerBuilder builder)
        {
            builder.Register<GameInput>(Lifetime.Singleton);
            builder.Register<IInputService, InputService>(Lifetime.Singleton);
            builder.RegisterInstance(new BaseLockService<InputLockTag>().WithDebug());
        }

        private static void InstallTime(IContainerBuilder builder)
        {
            builder.Register<ITimeService, TimeService>(Lifetime.Singleton);
            builder.Register<IRealClock, RealClock>(Lifetime.Singleton);
            builder.Register<IGameClock, GameClock>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TimerService>().As<ITimerService>();
        }

        private static void InstallAudio(IContainerBuilder builder, CoreConfig config, AudioSourceSet audioSources)
        {
            builder.RegisterEntryPoint(_ => new AudioService(config.AudioMixer, audioSources.SfxSources, audioSources.MusicSourceA, audioSources.MusicSourceB), Lifetime.Singleton).As<IAudioService>();
        }

        private static void InstallLocalization(IContainerBuilder builder, CoreConfig config)
        {
            builder.Register(_ => new LocalizationService(config.DefaultLanguage, config.SupportedLanguages), Lifetime.Singleton).As<ILocalizationService>().AsSelf();
            builder.RegisterLocalizationTable(config.SharedText);
            builder.RegisterEntryPoint(CreateRootLabelBinder, Lifetime.Singleton);
        }

        private static void InstallSettings(IContainerBuilder builder, CoreConfig config)
        {
            builder.Register<IGraphicsDevice, UnityGraphicsDevice>(Lifetime.Singleton);
            builder.Register(resolver => CreateSettingsService(resolver, config), Lifetime.Singleton).As<ISettingsService>().As<ISettingsLoader>();
        }

        private static void InstallDomains(IContainerBuilder builder)
        {
            builder.Register<DomainRunner>(Lifetime.Singleton);
        }

        private static LocalizedLabelBinder CreateRootLabelBinder(IObjectResolver resolver)
        {
            var rootScope = resolver.Resolve<ScopeRef>().Scope;
            return new LocalizedLabelBinder(resolver.Resolve<ILocalizationService>(), [rootScope.gameObject], Observable.Empty<Scene>());
        }

        private static SettingsService CreateSettingsService(IObjectResolver resolver, CoreConfig config)
        {
            var defaults = new SettingsDefaults(config.DefaultMasterVolume, config.DefaultMusicVolume, config.DefaultSfxVolume, config.DefaultUiVolume, config.DefaultLanguage);

            return new SettingsService(
                resolver.Resolve<IFileStorage>(),
                resolver.Resolve<IJsonSerializer>(),
                resolver.Resolve<IAudioService>(),
                resolver.Resolve<ILocalizationService>(),
                resolver.Resolve<IInputService>(),
                resolver.Resolve<IGraphicsDevice>(),
                defaults,
                config.SupportedLanguages);
        }

        private static ContentDirectoryRegistry CreateContentDirectoryRegistry()
        {
            var registry = new ContentDirectoryRegistry(new UnityContentLoadManager());
#if !UNITY_EDITOR
            registry.RegisterAll(System.IO.Path.Combine(Application.streamingAssetsPath, ContentDirectoryRegistry.RootFolderName));
#endif
            return registry;
        }
    }
}
