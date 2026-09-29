using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Collectibles
{
    internal sealed class PickupEffectView : MonoBehaviour
    {
        [SerializeField, Required] private ParticleSystem _particles = null!;

        public async UniTask PlayAsync(Vector3 position, CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            transform.position = position;
            _particles.Clear(true);
            _particles.Play(true);
            await UniTask.WaitWhile(() => _particles.IsAlive(true), cancellationToken: linkedCts.Token);
        }
    }
}
