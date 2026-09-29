namespace Gameplay.Progress
{
    internal sealed record GameplaySaveDto(int BestScore, int RoundsPlayed)
    {
        public static readonly GameplaySaveDto Empty = new(BestScore: 0, RoundsPlayed: 0);
    }
}
