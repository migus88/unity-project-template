using System;
using System.Threading;
using Core.Audio;
using Core.Domains;
using Core.Input;
using Core.Localization;
using Core.Logging;
using Cysharp.Threading.Tasks;
using Gameplay.Cameras;
using Gameplay.Collectibles;
using Gameplay.Player;
using Gameplay.Progress;
using Gameplay.Room;
using Gameplay.Round;
using Migs.MLock.Interfaces;
using R3;
using VContainer.Unity;

namespace Gameplay.Flow
{
    internal sealed class GameplayFlowPresenter : IAsyncStartable, IDisposable
    {
        private IDisposable? _inputMaps;

        private readonly GameplayArgs _args;
        private readonly GameplayContent _content;
        private readonly GameplayConfig _config;
        private readonly DomainSceneSet _scenes;
        private readonly IInputService _input;
        private readonly ILockService<InputLockTag> _locks;
        private readonly IAudioService _audio;
        private readonly ILocalizationService _localization;
        private readonly PlayerView _player;
        private readonly GameplayCameraView _camera;
        private readonly RoundResultView _resultView;
        private readonly CollectiblesPresenter _collectibles;
        private readonly RoundService _round;
        private readonly GameplayProgressService _progress;
        private readonly DomainCompletion<GameplayResult> _completion;

        public GameplayFlowPresenter(
            GameplayArgs args,
            GameplayContent content,
            GameplayConfig config,
            DomainSceneSet scenes,
            IInputService input,
            ILockService<InputLockTag> locks,
            IAudioService audio,
            ILocalizationService localization,
            PlayerView player,
            GameplayCameraView camera,
            RoundResultView resultView,
            CollectiblesPresenter collectibles,
            RoundService round,
            GameplayProgressService progress,
            DomainCompletion<GameplayResult> completion)
        {
            _args = args;
            _content = content;
            _config = config;
            _scenes = scenes;
            _input = input;
            _locks = locks;
            _audio = audio;
            _localization = localization;
            _player = player;
            _camera = camera;
            _resultView = resultView;
            _collectibles = collectibles;
            _round = round;
            _progress = progress;
            _completion = completion;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            _inputMaps = _input.Push(InputMaps.Player | InputMaps.Ui);

            var room = await LoadRoomAsync(ct);
            _player.Teleport(room.PlayerSpawnPosition);
            _camera.SetFollowTarget(_player.CameraTarget);
            _collectibles.Begin(room);

            var result = await _round.RunAsync(room.Collectibles.Count, _config.RoundDuration, ct);
            using var inputLock = _locks.Lock(InputLockTag.Movement);

            await result.Match(
                won => ShowRoundEndAsync(GameplayText.YouWon, won.Score, _config.WinCue, ct),
                lost => ShowRoundEndAsync(GameplayText.YouLost, lost.Score, _config.LoseCue, ct),
                quitToMenu => UniTask.CompletedTask);

            _completion.Complete(result);
        }

        private async UniTask<RoomView> LoadRoomAsync(CancellationToken ct)
        {
            if (_args.LevelIndex < 0 || _args.LevelIndex >= _content.EnvironmentScenes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(_args.LevelIndex), _args.LevelIndex, $"Gameplay has {_content.EnvironmentScenes.Length} levels.");
            }

            var loaded = await _scenes.LoadAsync(_content.EnvironmentScenes[_args.LevelIndex], ct);

            if (!loaded.TryPickT0(out var scene, out _))
            {
                throw new InvalidOperationException($"The environment scene of level {_args.LevelIndex} was not found.");
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<RoomView>(out var room))
                {
                    return room;
                }
            }

            throw new InvalidOperationException($"Scene '{scene.name}' has no {nameof(RoomView)} on a root object.");
        }

        private async UniTask ShowRoundEndAsync(TextKey title, int score, AudioCue cue, CancellationToken ct)
        {
            _audio.Play(cue);

            var record = _progress.RecordRound(score);
            var saved = await _progress.SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Gameplay, $"Gameplay progress could not be saved: {error.Message}");
            }

            _resultView.Show(
                _localization.Get(title),
                _localization.Format(GameplayText.Score, score),
                _localization.Format(GameplayText.BestScore, record.BestScore),
                record.IsNewBestScore);

            await _resultView.ContinueClicked.FirstAsync(ct);
            _resultView.SetInteractable(false);
        }

        public void Dispose()
        {
            _inputMaps?.Dispose();
        }
    }
}
