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
        private Transform? _effectsRoot;
        private IViewPool<PickupEffectView>? _effects;
        private DisposableBag _subscriptions;

        private readonly RoundService _round;
        private readonly GameplayConfig _config;
        private readonly GameplayContent _content;
        private readonly IAudioService _audio;
        private readonly ViewPoolFactory _viewPools;

        public CollectiblesPresenter(RoundService round, GameplayConfig config, GameplayContent content, IAudioService audio, ViewPoolFactory viewPools)
        {
            _round = round;
            _config = config;
            _content = content;
            _audio = audio;
            _viewPools = viewPools;
        }

        public async UniTask BeginAsync(RoomView room, CancellationToken ct)
        {
            if (_effects != null)
            {
                throw new InvalidOperationException("Collectibles have already begun.");
            }

            _effectsRoot = room.EffectsRoot;
            _effects = _viewPools.Create<PickupEffectView>(_content.PickupEffect);

            foreach (var collectible in room.Collectibles)
            {
                collectible.Touched
                    .SubscribeAwait((_, collectCt) => CollectAsync(collectible, collectCt).AsValueTask(), AwaitOperation.Drop)
                    .AddTo(ref _subscriptions);
            }

            await PrewarmEffectAsync(ct);
        }

        private async UniTask CollectAsync(CollectibleView collectible, CancellationToken ct)
        {
            if (!_round.IsRunning)
            {
                return;
            }

            var position = collectible.Position;
            collectible.Hide();
            _audio.PlayAt(_config.CollectCue, position);
            _round.Collect(_config.PointsPerCollectible);
            await PlayPickupEffectAsync(position, ct);
        }

        private async UniTask PrewarmEffectAsync(CancellationToken ct)
        {
            var effect = await RentEffectAsync(ct);

            if (effect != null)
            {
                _effects!.Return(effect);
            }
        }

        private async UniTask PlayPickupEffectAsync(Vector3 position, CancellationToken ct)
        {
            var effect = await RentEffectAsync(ct);

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
                _effects!.Return(effect);
            }
        }

        private async UniTask<PickupEffectView?> RentEffectAsync(CancellationToken ct)
        {
            var rented = await _effects!.RentAsync(_effectsRoot!, ct);

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
            _effects?.Dispose();
        }
    }
}
