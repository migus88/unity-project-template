using System.Collections.Generic;
using System.IO;
using Core.Analytics;
using Core.Audio;
using Core.Content;
using Core.Diagnostics;
using Core.Domains;
using Core.Environment;
using Core.Http;
using Core.Input;
using Core.Localization;
using Core.Logging;
using Core.Privacy;
using Core.RemoteConfig;
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
        private const int InputLockCapacity = 16;
        private const int InputLockableCapacity = 64;

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
            InstallLogging(builder);
            builder.RegisterInstance(config);
            InstallApplication(builder);
            InstallAnalytics(builder);
            InstallStorage(builder, storageRoot);
            InstallDiagnostics(builder, storageRoot);
            InstallHttp(builder);
            InstallRemoteConfig(builder);
            InstallSave(builder);
            InstallContent(builder);
            InstallInput(builder);
            InstallTime(builder);
            InstallAudio(builder, config, audioSources);
            InstallLocalization(builder, config);
            InstallSettings(builder, config);
            InstallPrivacy(builder);
            InstallDomains(builder);
            builder.Register<CoreStartup>(Lifetime.Singleton);
        }

        private static void InstallLogging(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<UnhandledExceptionForwarder>();
        }

        private static void InstallApplication(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ApplicationService>().As<IApplicationService>();
            builder.Register(_ => new ReportingRule(Application.isEditor, new EditorSessionReportingOverride()), Lifetime.Singleton);
        }

        private static void InstallAnalytics(IContainerBuilder builder)
        {
            builder.Register<IAnalytics, AnalyticsService>(Lifetime.Singleton);
            builder.Register<AnalyticsFlushSignal>(Lifetime.Singleton);
        }

        private static void InstallStorage(IContainerBuilder builder, string storageRoot)
        {
            builder.Register<IFileStorage>(_ => new FileStorage(storageRoot), Lifetime.Singleton);
            builder.Register<IJsonSerializer, JsonSerializer>(Lifetime.Singleton);
        }

        private static void InstallDiagnostics(IContainerBuilder builder, string storageRoot)
        {
            builder.Register(_ => new CrashContext(storageRoot, Application.consoleLogPath), Lifetime.Singleton).As<ICrashContext>().AsSelf();
        }

        private static void InstallHttp(IContainerBuilder builder)
        {
            builder.Register<IHttpService>(_ => new HttpService(new UnityWebRequestTransport()), Lifetime.Singleton);
        }

        private static void InstallRemoteConfig(IContainerBuilder builder)
        {
            builder.Register(CreateRemoteConfigService, Lifetime.Singleton).As<IRemoteConfigService>().AsSelf();
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
            builder.RegisterInstance(new BaseLockService<InputLockTag>(InputLockCapacity, InputLockableCapacity).WithDebug());
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

        private static void InstallPrivacy(IContainerBuilder builder)
        {
            builder.Register<ConsentService>(Lifetime.Singleton).As<IConsentService>().AsSelf();
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

        private static RemoteConfigService CreateRemoteConfigService(IObjectResolver resolver)
        {
            return new RemoteConfigService(
                resolver.Resolve<IHttpService>(),
                resolver.Resolve<IFileStorage>(),
                resolver.Resolve<IJsonSerializer>(),
                CreateRemoteConfigSource(resolver));
        }

        private static RemoteConfigSource CreateRemoteConfigSource(IObjectResolver resolver)
        {
            var application = resolver.Resolve<IApplicationService>();
            var platform = application.Platform.ToString();

            if (!resolver.TryResolve<BuildEnvironment>(out var environment))
            {
                return RemoteConfigSource.None(application.Version, platform);
            }

            var defaults = new List<KeyValuePair<string, string>>(environment.RemoteConfigDefaults.Count);

            foreach (var entry in environment.RemoteConfigDefaults)
            {
                defaults.Add(new KeyValuePair<string, string>(entry.Key, entry.Value));
            }

            return new RemoteConfigSource(environment.TelemetryEndpoint, environment.EnvironmentName, defaults, application.Version, platform);
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
