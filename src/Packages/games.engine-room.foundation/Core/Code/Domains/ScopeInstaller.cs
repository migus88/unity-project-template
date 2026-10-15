using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.Domains
{
    public abstract class ScopeInstaller : MonoBehaviour, IInstaller
    {
        public abstract void Install(IContainerBuilder builder);
    }
}
