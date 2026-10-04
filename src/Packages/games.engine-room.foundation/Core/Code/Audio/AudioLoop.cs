using System;

namespace Core.Audio
{
    public readonly struct AudioLoop : IDisposable
    {
        public bool IsPlaying => _service != null && _service.IsLoopPlaying(_id);

        private readonly AudioService? _service;
        private readonly int _id;

        internal AudioLoop(AudioService service, int id)
        {
            _service = service;
            _id = id;
        }

        public void Stop()
        {
            _service?.StopLoop(_id);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
