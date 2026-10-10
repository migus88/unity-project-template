#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using Core.Analytics;
using Core.Input;
using Core.Logging;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;
using R3;
using VContainer.Unity;

namespace Core.Cheats
{
    internal sealed class CheatConsolePresenter : IStartable, IDisposable
    {
        private IDisposable? _inputMaps;
        private IDisposable? _inputLock;
        private IDisposable? _clearCheat;
        private DisposableBag _subscriptions;
        private string? _assignedLine;

        private readonly CheatConsoleView _view;
        private readonly CheatRegistry _registry;
        private readonly CheatConsoleModel _model;
        private readonly IInputService _input;
        private readonly ILockService<InputLockTag> _locks;
        private readonly IAnalytics _analytics;

        public CheatConsolePresenter(CheatConsoleView view, CheatRegistry registry, CheatConsoleModel model, IInputService input, ILockService<InputLockTag> locks, IAnalytics analytics)
        {
            _view = view;
            _registry = registry;
            _model = model;
            _input = input;
            _locks = locks;
            _analytics = analytics;
        }

        public void Start()
        {
            _view.Hide();
            _clearCheat = _registry.Add(new ClearConsoleCheat(_model));
            _view.Submitted
                .SubscribeAwait((line, ct) => SubmitAsync(line, ct).AsValueTask(), AwaitOperation.Sequential)
                .AddTo(ref _subscriptions);
            _view.Entered
                .Subscribe(Enter)
                .AddTo(ref _subscriptions);
            _view.LineChanged
                .Subscribe(EditLine)
                .AddTo(ref _subscriptions);
        }

        public void Toggle()
        {
            if (_view.IsShown)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (_view.IsShown)
            {
                return;
            }

            _view.Show();
            _inputMaps = _input.Push(InputMaps.None);
            _inputLock = _locks.LockAll();
            ShowSuggestions(_view.Line);
        }

        public void Close()
        {
            if (!_view.IsShown)
            {
                return;
            }

            _view.Hide();
            ReleaseInput();
        }

        public void CompleteLine()
        {
            if (!_view.IsShown)
            {
                return;
            }

            SetLine(_model.AcceptSelection(_view.Line) ?? _model.Complete(_view.Line));
        }

        public void MoveUp()
        {
            Move(-1);
        }

        public void MoveDown()
        {
            Move(1);
        }

        public void Cancel()
        {
            if (!_view.IsShown)
            {
                return;
            }

            if (!_model.HasSuggestions(_view.Line))
            {
                Close();
                return;
            }

            _model.DismissSuggestions();
            ShowSuggestions(_view.Line);
            _view.Focus();
        }

        public void Enter(string line)
        {
            var accepted = _view.IsShown ? _model.AcceptSelection(line) : null;

            if (accepted == null)
            {
                _view.Submit(line);
                return;
            }

            SetLine(accepted);
            _view.Focus();
        }

        private void Move(int step)
        {
            if (!_view.IsShown)
            {
                return;
            }

            var line = _view.Line;

            if (_model.HasSuggestions(line))
            {
                _model.MoveSelection(line, step);
                ShowSuggestions(line);
                _view.KeepCaretAtEnd();
                return;
            }

            var recalled = step < 0 ? _model.RecallPrevious(line) : _model.RecallNext();

            if (recalled != null)
            {
                SetLine(recalled);
            }
        }

        private void EditLine(string line)
        {
            if (line != _assignedLine)
            {
                _model.EditLine();
            }

            _assignedLine = null;
            ShowSuggestions(line);
        }

        private async UniTask SubmitAsync(string line, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                ResetLine();
                return;
            }

            _model.AddCommand(line);
            var command = _registry.Find(CheatLine.Parse(line).Name)?.Name;
            var reply = await ExecuteAsync(line, ct);
            TrackUse(command);
            Log.Info(LogTags.Cheats, reply.Length == 0 ? $"> {line}" : $"> {line}\n{reply}");
            _model.AddReply(reply);
            _view.LastReply = reply;
            _view.ShowOutput(_model.Output);
            ResetLine();
        }

        private async UniTask<string> ExecuteAsync(string line, CancellationToken ct)
        {
            try
            {
                return await _registry.ExecuteAsync(line, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Exception(exception);
                return $"Error: {exception.Message}";
            }
        }

        private void TrackUse(string? command)
        {
            if (command != null)
            {
                _analytics.Track(new CheatUsedEvent(command));
            }
        }

        private void ResetLine()
        {
            if (!_view.IsShown)
            {
                return;
            }

            SetLine(string.Empty);
            _view.Focus();
        }

        private void SetLine(string line)
        {
            _assignedLine = line;
            _view.SetLine(line);
            ShowSuggestions(line);
        }

        private void ShowSuggestions(string line)
        {
            var suggestions = _model.Suggest(line);
            _view.ShowSuggestions(suggestions.Values, suggestions.Selected);
        }

        private void ReleaseInput()
        {
            _inputLock?.Dispose();
            _inputLock = null;
            _inputMaps?.Dispose();
            _inputMaps = null;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _clearCheat?.Dispose();
            ReleaseInput();
        }
    }
}
#endif
