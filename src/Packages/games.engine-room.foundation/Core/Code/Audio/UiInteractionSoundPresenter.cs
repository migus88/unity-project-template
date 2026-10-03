using System;
using Core.Time;
using R3;
using VContainer.Unity;

namespace Core.Audio
{
    internal sealed class UiInteractionSoundPresenter : IStartable, IDisposable
    {
        public static readonly TimeSpan MinTickInterval = TimeSpan.FromMilliseconds(75);

        private readonly UiInteractionRelay _relay;
        private readonly IUiInteractionSounds _sounds;
        private readonly IRealClock _clock;

        private DisposableBag _subscriptions;
        private DateTime? _lastTickAt;

        public UiInteractionSoundPresenter(UiInteractionRelay relay, IUiInteractionSounds sounds, IRealClock clock)
        {
            _relay = relay;
            _sounds = sounds;
            _clock = clock;
        }

        public void Start()
        {
            _relay.Requested.Subscribe(Play).AddTo(ref _subscriptions);
        }

        private void Play(UiInteraction interaction)
        {
            if (interaction == UiInteraction.Tick && !IsTickDue())
            {
                return;
            }

            _sounds.Play(interaction);
        }

        private bool IsTickDue()
        {
            var now = _clock.UtcNow;
            if (_lastTickAt.HasValue && now - _lastTickAt.Value < MinTickInterval)
            {
                return false;
            }

            _lastTickAt = now;
            return true;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
