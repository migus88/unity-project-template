using System.Collections.Generic;
using Gameplay.Collectibles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Room
{
    internal sealed class RoomView : MonoBehaviour
    {
        [SerializeField, Required] private Transform _playerSpawn = null!;
        [SerializeField, Required] private Transform _effectsRoot = null!;
        [SerializeField, Required] private CollectibleView[] _collectibles = [];

        public Vector3 PlayerSpawnPosition => _playerSpawn.position;
        public Transform EffectsRoot => _effectsRoot;
        public IReadOnlyList<CollectibleView> Collectibles => _collectibles;
    }
}
