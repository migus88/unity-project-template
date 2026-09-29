using System;
using VContainer.Unity;

namespace Loading
{
    internal sealed class LoadingScreenPresenter : IInitializable, IDisposable
    {
        private IDisposable? _attachment;

        private readonly LoadingScreen _screen;
        private readonly LoadingScreenView _view;

        public LoadingScreenPresenter(LoadingScreen screen, LoadingScreenView view)
        {
            _screen = screen;
            _view = view;
        }

        public void Initialize()
        {
            _attachment = _screen.Attach(_view);
        }

        public void Dispose()
        {
            _attachment?.Dispose();
        }
    }
}
