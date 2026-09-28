using System;
using Core;
using Core.Domains;
using Core.Logging;
using Core.Transitions;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Bootstrap
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField, Required] private CoreConfig _coreConfig = null!;
        [SerializeField, Required] private TransitionOverlayView _transitionOverlay = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new ScopeRef(this, 0));
            CoreInstaller.Install(builder, _coreConfig, _transitionOverlay);

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
#endif
                default:
                    throw new InvalidOperationException($"Unsupported boot mode {BootMode.Current}.");
            }
        }
    }
}
