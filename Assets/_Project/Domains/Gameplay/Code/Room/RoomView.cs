using System.Collections.Generic;
using Gameplay.Collectibles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Room
{
    internal sealed class RoomView : MonoBehaviour
    {
        public Vector3 PlayerSpawnPosition => _playerSpawn.position;
        public IReadOnlyList<CollectibleView> Collectibles => _collectibles;

        [SerializeField, Required] private Transform _playerSpawn = null!;
        [SerializeField, Required] private CollectibleView[] _collectibles = [];
    }
}
