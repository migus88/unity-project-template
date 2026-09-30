using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Gameplay.Collectibles
{
    internal sealed class PickupEffectView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particles = null!;

        public async UniTask PlayAsync(CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            _particles.Clear(true);
            _particles.Play(true);
            await UniTask.WaitWhile(() => _particles.IsAlive(true), cancellationToken: linkedCts.Token);
        }
    }
}
