using System;
using R3;

namespace Core.Time
{
    public sealed class TimeService : ITimeService, IDisposable
    {
        public ReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

        public float TimeScale
        {
            get => _timeScale;
            set
            {
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Time scale cannot be negative.");
                }

                _timeScale = value;
                Apply();
            }
        }

        private float _timeScale = 1f;
        private int _pauseCount;

        private readonly ReactiveProperty<bool> _isPaused = new(false);

        public IDisposable Pause()
        {
            _pauseCount++;
            Apply();
            return new PauseHandle(this);
        }

        private void Resume()
        {
            _pauseCount--;
            Apply();
        }

        private void Apply()
        {
            var isPaused = _pauseCount > 0;
            UnityEngine.Time.timeScale = isPaused ? 0f : _timeScale;

            if (!_isPaused.IsDisposed)
            {
                _isPaused.Value = isPaused;
            }
        }

        public void Dispose()
        {
            _isPaused.Dispose();
        }

        private sealed class PauseHandle : IDisposable
        {
            private bool _isDisposed;

            private readonly TimeService _owner;

            public PauseHandle(TimeService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _owner.Resume();
            }
        }
    }
}
