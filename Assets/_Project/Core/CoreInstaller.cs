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
            InstallDomains(builder);
        }

        private static void InstallStorage(IContainerBuilder builder)
        {
            builder.Register<IFileStorage>(_ => new FileStorage(Application.persistentDataPath), Lifetime.Singleton);
            builder.Register<IJsonSerializer, JsonSerializer>(Lifetime.Singleton);
        }

        private static void InstallDomains(IContainerBuilder builder)
        {
            builder.Register<DomainRunner>(Lifetime.Singleton);
        }
    }
}
