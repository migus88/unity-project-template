#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
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

        private readonly CheatConsoleView _view;
        private readonly CheatRegistry _registry;
        private readonly CheatConsoleModel _model;
        private readonly IInputService _input;
        private readonly ILockService<InputLockTag> _locks;

        public CheatConsolePresenter(CheatConsoleView view, CheatRegistry registry, CheatConsoleModel model, IInputService input, ILockService<InputLockTag> locks)
        {
            _view = view;
            _registry = registry;
            _model = model;
            _input = input;
            _locks = locks;
        }

        public void Start()
        {
            _view.Hide();
            _clearCheat = _registry.Add(new ClearConsoleCheat(_model));
            _view.Submitted
                .SubscribeAwait((line, ct) => SubmitAsync(line, ct).AsValueTask(), AwaitOperation.Sequential)
                .AddTo(ref _subscriptions);
            _view.LineChanged
                .Subscribe(ShowSuggestions)
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

            SetLine(_model.Complete(_view.Line));
        }

        public void RecallPrevious()
        {
            if (!_view.IsShown)
            {
                return;
            }

            var line = _model.RecallPrevious(_view.Line);

            if (line != null)
            {
                SetLine(line);
            }
        }

        public void RecallNext()
        {
            if (!_view.IsShown)
            {
                return;
            }

            var line = _model.RecallNext();

            if (line != null)
            {
                SetLine(line);
            }
        }

        private async UniTask SubmitAsync(string line, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                ResetLine();
                return;
            }

            _model.AddCommand(line);
            var reply = await ExecuteAsync(line, ct);
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
