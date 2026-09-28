#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Core;
using Core.Domains;
using Core.Logging;
using Cysharp.Threading.Tasks;
using UnityEditor;
using VContainer.Unity;

namespace Bootstrap
{
    internal sealed class DebugDomainBoot : IAsyncStartable
    {
        private readonly CoreStartup _coreStartup;
        private readonly IReadOnlyList<IDebugRunnableDomain> _domains;

        public DebugDomainBoot(CoreStartup coreStartup, IReadOnlyList<IDebugRunnableDomain> domains)
        {
            _coreStartup = coreStartup;
            _domains = domains;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var domain = FindDomain(BootMode.DebugScopeScenePath);
            var domainName = domain.GetType().Name;

            await _coreStartup.RunAsync(ct);

            Log.Info(LogTags.Boot, $"Debug-running {domainName}.");
            var result = await domain.RunDebugAsync(ct);
            Log.Info(LogTags.Boot, $"{domainName} finished with {result}.");

            EditorApplication.isPlaying = false;
        }

        private IDebugRunnableDomain FindDomain(string scopeScenePath)
        {
            foreach (var domain in _domains)
            {
                if (EditorScopeScene.IsScopeSceneOf(domain.Descriptor, scopeScenePath))
                {
                    return domain;
                }
            }

            throw new InvalidOperationException($"No domain registered in the root scope has '{scopeScenePath}' as its scope scene.");
        }
    }
}
#endif
