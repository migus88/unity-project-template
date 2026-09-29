using Core.Domains;
using Unity.Loading;
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(menuName = "Domains/Gameplay Content")]
    internal sealed class GameplayContent : DomainContent
    {
        [field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];
    }
}
