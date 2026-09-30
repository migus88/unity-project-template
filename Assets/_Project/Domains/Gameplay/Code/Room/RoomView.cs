using System.Collections.Generic;
using Gameplay.Collectibles;
using UnityEngine;

namespace Gameplay.Room
{
    internal sealed class RoomView : MonoBehaviour
    {
        public Vector3 PlayerSpawnPosition => _playerSpawn.position;
        public IReadOnlyList<CollectibleView> Collectibles => _collectibles;

        [SerializeField] private Transform _playerSpawn = null!;
        [SerializeField] private CollectibleView[] _collectibles = [];
    }
}
