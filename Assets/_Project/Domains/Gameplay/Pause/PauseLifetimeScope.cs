using Core.Domains;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Gameplay.Pause
{
    internal sealed class PauseLifetimeScope : DomainLifetimeScope
    {
        [SerializeField, Required] private PauseView _view = null!;

        protected override void ConfigureDomain(IContainerBuilder builder)
        {
            builder.RegisterComponent(_view);
            builder.Register<ResumeRequests>(Lifetime.Singleton);
            builder.RegisterEntryPoint<PausePresenter>();
            builder.RegisterEntryPoint<PauseInputHandler>();
        }
    }
}
