using System;
using R3;

namespace Gameplay.Pause
{
    internal sealed class ResumeRequests : IDisposable
    {
        public Observable<Unit> Requested => _requested;

        private readonly Subject<Unit> _requested = new();

        public void Request()
        {
            _requested.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            _requested.Dispose();
        }
    }
}
