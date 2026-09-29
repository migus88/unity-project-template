using System;
using R3;

namespace Gameplay.Round
{
    internal sealed class ScoreModel : IDisposable
    {
        public ReadOnlyReactiveProperty<int> Score => _score;

        private readonly ReactiveProperty<int> _score = new(0);

        public void Add(int points)
        {
            if (points <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(points), points, "Points must be positive.");
            }

            _score.Value += points;
        }

        public void Dispose()
        {
            _score.Dispose();
        }
    }
}
