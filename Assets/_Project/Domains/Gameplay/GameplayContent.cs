using Core.Domains;
using Unity.Loading;
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(menuName = "Domains/Gameplay Content")]
    internal sealed class GameplayContent : DomainContent
    {
        [field: SerializeField] public LoadableSceneId[] EnvironmentScenes { get; private set; } = [];
        [field: SerializeField] public Loadable<GameObject> PickupEffect { get; private set; } = null!;
    }
}
