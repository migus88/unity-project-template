using System;
using System.Threading;
using Core.Logging;
using Core.Results;
using Core.Save;
using Cysharp.Threading.Tasks;
using OneOf;
using Success = OneOf.Types.Success;

namespace Gameplay.Progress
{
    internal sealed class GameplayProgressService
    {
        private readonly ISaveStore _saveStore;

        public GameplayProgressService(ISaveStore saveStore)
        {
            _saveStore = saveStore;
        }

        public RoundRecord RecordRound(int score)
        {
            if (score < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), score, "Score cannot be negative.");
            }

            var progress = Load();
            var updated = new GameplaySaveDto(Math.Max(progress.BestScore, score), progress.RoundsPlayed + 1);
            _saveStore.Write(GameplaySave.Section, updated);
            return new RoundRecord(updated.BestScore, score > progress.BestScore);
        }

        public UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            return _saveStore.FlushAsync(ct);
        }

        private GameplaySaveDto Load()
        {
            return _saveStore.Read(GameplaySave.Section).Match(
                progress => progress,
                notFound => GameplaySaveDto.Empty,
                corrupted =>
                {
                    Log.Warn(LogTags.Gameplay, $"Gameplay progress is corrupted and starts over: {corrupted.Reason}");
                    return GameplaySaveDto.Empty;
                });
        }
    }
}
