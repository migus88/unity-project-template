using System;
using Core.Domains;
using Core.Input;
using Core.Time;
using R3;
using VContainer.Unity;

namespace Gameplay.Pause
{
    internal sealed class PausePresenter : IStartable, IDisposable
    {
        private readonly PauseView _view;
        private readonly ResumeRequests _resumeRequests;
        private readonly IInputService _input;
        private readonly ITimeService _time;
        private readonly DomainCompletion<PauseResult> _completion;

        private DisposableBag _subscriptions;
        private IDisposable? _inputMaps;
        private IDisposable? _timePause;

        public PausePresenter(PauseView view, ResumeRequests resumeRequests, IInputService input, ITimeService time, DomainCompletion<PauseResult> completion)
        {
            _view = view;
            _resumeRequests = resumeRequests;
            _input = input;
            _time = time;
            _completion = completion;
        }

        public void Start()
        {
            _inputMaps = _input.Push(InputMaps.Ui);
            _timePause = _time.Pause();

            _view.ResumeClicked.Subscribe(_ => Complete(new PauseResult.Resume())).AddTo(ref _subscriptions);
            _resumeRequests.Requested.Subscribe(_ => Complete(new PauseResult.Resume())).AddTo(ref _subscriptions);
            _view.SettingsClicked.Subscribe(_ => Complete(new PauseResult.OpenSettings())).AddTo(ref _subscriptions);
            _view.QuitToMenuClicked.Subscribe(_ => Complete(new PauseResult.QuitToMenu())).AddTo(ref _subscriptions);
        }

        private void Complete(PauseResult result)
        {
            if (_completion.IsCompleted)
            {
                return;
            }

            _view.SetInteractable(false);
            _completion.Complete(result);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _timePause?.Dispose();
            _inputMaps?.Dispose();
        }
    }
}
