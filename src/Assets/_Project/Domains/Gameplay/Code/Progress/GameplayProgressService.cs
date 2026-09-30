using System;
using System.Collections.Generic;
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
            var updated = new GameplayProgress(Math.Max(progress.BestScore, score), progress.RoundsPlayed + 1);
            _saveStore.Write(GameplaySave.Section, new GameplaySaveDto(updated.BestScore, updated.RoundsPlayed));
            return new RoundRecord(updated.BestScore, score > progress.BestScore);
        }

        public UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            return _saveStore.FlushAsync(ct);
        }

        private GameplayProgress Load()
        {
            return _saveStore.Read(GameplaySave.Section).Match(
                ToProgress,
                notFound => CreateDefaultProgress(),
                corrupted =>
                {
                    Log.Warn(LogTags.Gameplay, $"Gameplay progress is corrupted and starts over: {corrupted.Reason}");
                    return CreateDefaultProgress();
                });
        }

        private static GameplayProgress ToProgress(GameplaySaveDto dto)
        {
            var missingFields = new List<string>();

            if (dto.BestScore is null)
            {
                missingFields.Add("bestScore");
            }

            if (dto.RoundsPlayed is null)
            {
                missingFields.Add("roundsPlayed");
            }

            if (missingFields.Count > 0)
            {
                Log.Warn(LogTags.Gameplay, $"Stored gameplay progress is missing {string.Join(", ", missingFields)}, using the defaults for them.");
            }

            return new GameplayProgress(
                dto.BestScore ?? GameplaySave.DefaultBestScore,
                dto.RoundsPlayed ?? GameplaySave.DefaultRoundsPlayed);
        }

        private static GameplayProgress CreateDefaultProgress()
        {
            return new GameplayProgress(GameplaySave.DefaultBestScore, GameplaySave.DefaultRoundsPlayed);
        }
    }
}
