using System.IO;
using Core.Content;
using Core.Domains;
using Core.Storage;
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
