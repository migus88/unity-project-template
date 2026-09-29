using System;
using System.Threading;
using Core.Audio;
using Core.Logging;
using Core.Views;
using Cysharp.Threading.Tasks;
using Gameplay.Room;
using Gameplay.Round;
using R3;
using UnityEngine;

namespace Gameplay.Collectibles
{
    internal sealed class CollectiblesPresenter : IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly RoundService _round;
        private readonly GameplayConfig _config;
        private readonly IAudioService _audio;
        private readonly IViewPool<PickupEffectView> _effects;

        public CollectiblesPresenter(RoundService round, GameplayConfig config, GameplayContent content, IAudioService audio, ViewPoolFactory viewPools)
        {
            _round = round;
            _config = config;
            _audio = audio;
            _effects = viewPools.Create<PickupEffectView>(content.PickupEffect);
        }

        public async UniTask BeginAsync(RoomView room, CancellationToken ct)
        {
            foreach (var collectible in room.Collectibles)
            {
                collectible.Touched
                    .SubscribeAwait((_, collectCt) => CollectAsync(collectible, room.EffectsRoot, collectCt).AsValueTask(), AwaitOperation.Drop)
                    .AddTo(ref _subscriptions);
            }

            await PrewarmEffectAsync(room.EffectsRoot, ct);
        }

        private async UniTask CollectAsync(CollectibleView collectible, Transform effectsRoot, CancellationToken ct)
        {
            if (!_round.IsRunning)
            {
                return;
            }

            var position = collectible.Position;
            collectible.Hide();
            _audio.PlayAt(_config.CollectCue, position);
            _round.Collect(_config.PointsPerCollectible);
            await PlayPickupEffectAsync(position, effectsRoot, ct);
        }

        private async UniTask PrewarmEffectAsync(Transform effectsRoot, CancellationToken ct)
        {
            var effect = await RentEffectAsync(effectsRoot, ct);

            if (effect != null)
            {
                _effects.Return(effect);
            }
        }

        private async UniTask PlayPickupEffectAsync(Vector3 position, Transform effectsRoot, CancellationToken ct)
        {
            var effect = await RentEffectAsync(effectsRoot, ct);

            if (effect == null)
            {
                return;
            }

            try
            {
                await effect.PlayAsync(position, ct);
            }
            finally
            {
                _effects.Return(effect);
            }
        }

        private async UniTask<PickupEffectView?> RentEffectAsync(Transform effectsRoot, CancellationToken ct)
        {
            var rented = await _effects.RentAsync(effectsRoot, ct);

            if (!rented.TryPickT0(out var effect, out _))
            {
                Log.Warn(LogTags.Gameplay, "The pickup effect prefab could not be loaded, skipping the effect.");
                return null;
            }

            return effect;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _effects.Dispose();
        }
    }
}
