using System;
using R3;
using VContainer.Unity;

namespace Core.Analytics
{
    internal sealed class UiClickTrackingPresenter : IStartable, IDisposable
    {
        private readonly UiClickRelay _relay;
        private readonly IUiClickTracker _tracker;

        private DisposableBag _subscriptions;

        public UiClickTrackingPresenter(UiClickRelay relay, IUiClickTracker tracker)
        {
            _relay = relay;
            _tracker = tracker;
        }

        public void Start()
        {
            _relay.Clicked.Subscribe(_tracker.Track).AddTo(ref _subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
