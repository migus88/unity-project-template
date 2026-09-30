using System;
using System.Threading;
using Core.Audio;
using Cysharp.Threading.Tasks;
using Gameplay.Room;
using Gameplay.Round;
using R3;

namespace Gameplay.Collectibles
{
    internal sealed class CollectiblesPresenter : IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly RoundService _round;
        private readonly GameplayConfig _config;
        private readonly IAudioService _audio;

        public CollectiblesPresenter(RoundService round, GameplayConfig config, IAudioService audio)
        {
            _round = round;
            _config = config;
            _audio = audio;
        }

        public void Begin(RoomView room)
        {
            foreach (var collectible in room.Collectibles)
            {
                collectible.Touched
                    .SubscribeAwait((_, ct) => CollectAsync(collectible, ct).AsValueTask(), AwaitOperation.Drop)
                    .AddTo(ref _subscriptions);
            }
        }

        private async UniTask CollectAsync(CollectibleView collectible, CancellationToken ct)
        {
            if (!_round.IsRunning)
            {
                return;
            }

            collectible.Hide();
            _audio.PlayAt(_config.CollectCue, collectible.Position);
            _round.Collect(_config.PointsPerCollectible);
            await collectible.PickupEffect.PlayAsync(ct);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
