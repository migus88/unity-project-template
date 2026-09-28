using System.IO;
using Core.Audio;
using Core.Content;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Core.Save;
using Core.Storage;
using Core.Time;
using Core.Transitions;
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
        public static void Install(IContainerBuilder builder, CoreConfig config, TransitionOverlayView transitionOverlay, Transform audioSourceRoot)
        {
            builder.RegisterInstance(config);
            InstallStorage(builder);
            InstallSave(builder);
            InstallContent(builder);
            InstallInput(builder);
            InstallTime(builder);
            InstallAudio(builder, config, audioSourceRoot);
            InstallTransitions(builder, transitionOverlay);
            InstallLocalization(builder, config);
            InstallDomains(builder);
            builder.Register<CoreStartup>(Lifetime.Singleton);
        }

        private static void InstallStorage(IContainerBuilder builder)
        {
            builder.Register<IFileStorage>(_ => new FileStorage(Application.persistentDataPath), Lifetime.Singleton);
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

        private static void InstallAudio(IContainerBuilder builder, CoreConfig config, Transform audioSourceRoot)
        {
            builder.RegisterEntryPoint(_ => new AudioService(config.AudioMixer, audioSourceRoot, config.AudioSourcePoolSize), Lifetime.Singleton).As<IAudioService>();
        }

        private static void InstallTransitions(IContainerBuilder builder, TransitionOverlayView transitionOverlay)
        {
            builder.RegisterComponent<ITransitionOverlayView>(transitionOverlay);
            builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);
        }

        private static void InstallLocalization(IContainerBuilder builder, CoreConfig config)
        {
            builder.Register(_ => new LocalizationService(config.DefaultLanguage, config.SupportedLanguages), Lifetime.Singleton).As<ILocalizationService>().AsSelf();
            builder.RegisterLocalizationTable(config.SharedText);
            builder.RegisterEntryPoint(CreateRootLabelBinder, Lifetime.Singleton);
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

        private static ContentDirectoryRegistry CreateContentDirectoryRegistry()
        {
            var registry = new ContentDirectoryRegistry(new UnityContentLoadManager());
#if !UNITY_EDITOR
            registry.RegisterAll(Path.Combine(Application.streamingAssetsPath, ContentDirectoryRegistry.RootFolderName));
#endif
            return registry;
        }
    }
}
