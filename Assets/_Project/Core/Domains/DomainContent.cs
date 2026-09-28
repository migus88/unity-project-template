using Unity.Loading;
using UnityEngine;

namespace Core.Domains
{
    public abstract class DomainContent : ScriptableObject
    {
        [field: SerializeField] public LoadableSceneId ScopeScene { get; private set; }
    }
}
