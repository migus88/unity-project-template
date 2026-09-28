using System.IO;
using Core.Content;
using Core.Domains;
using Core.Input;
using Core.Storage;
using Migs.MLock;
using Migs.MLock.Debugging;
using UnityEngine;
using VContainer;

namespace Core
{
    public static class CoreInstaller
    {
        public static void Install(IContainerBuilder builder, CoreConfig config)
        {
            builder.RegisterInstance(config);
            InstallStorage(builder);
            InstallContent(builder);
            InstallInput(builder);
            InstallDomains(builder);
        }

        private static void InstallStorage(IContainerBuilder builder)
        {
            builder.Register<IFileStorage>(_ => new FileStorage(Application.persistentDataPath), Lifetime.Singleton);
            builder.Register<IJsonSerializer, JsonSerializer>(Lifetime.Singleton);
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

        private static void InstallDomains(IContainerBuilder builder)
        {
            builder.Register<DomainRunner>(Lifetime.Singleton);
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
