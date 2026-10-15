using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.Domains
{
    public abstract class DomainLifetimeScope : LifetimeScope
    {
        [SerializeField] private ScopeInstaller[] _installers = Array.Empty<ScopeInstaller>();

        protected sealed override void Configure(IContainerBuilder builder)
        {
            if (Parent == null)
            {
                throw CreateNotBuiltByRunnerException();
            }

            var parentDepth = Parent.Container.Resolve<ScopeRef>().Depth;
            builder.RegisterBuildCallback(EnsureBuiltByRunner);
            builder.RegisterDomainEntryPointFailureHandler();
            builder.RegisterInstance(new ScopeRef(this, parentDepth + 1));
            builder.Register<DomainSceneSet>(Lifetime.Singleton);
            ConfigureDomain(builder);
            RunInstallers(builder);
        }

        protected abstract void ConfigureDomain(IContainerBuilder builder);

        internal void OnApplicationQuit()
        {
            DisposeCore();
        }

        private void RunInstallers(IContainerBuilder builder)
        {
            for (var i = 0; i < _installers.Length; i++)
            {
                var installer = _installers[i];

                if (installer == null)
                {
                    throw new InvalidOperationException($"{GetType().Name} on '{name}' has an empty installer slot at index {i}.");
                }

                installer.Install(builder);
            }
        }

        private void EnsureBuiltByRunner(IObjectResolver resolver)
        {
            if (!resolver.TryResolve<IDomainCompletion>(out _))
            {
                throw CreateNotBuiltByRunnerException();
            }
        }

        private InvalidOperationException CreateNotBuiltByRunnerException()
        {
            return new InvalidOperationException($"{GetType().Name} was not built by {nameof(DomainRunner)}. Domain scopes must be built by {nameof(DomainRunner)}, so play from Bootstrap or from the domain's scope scene.");
        }
    }
}
