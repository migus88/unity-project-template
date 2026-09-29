using System;
using Gameplay.UserSettings;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace Gameplay.Cameras
{
    internal sealed class GameplayCameraPresenter : IStartable, IDisposable
    {
        private DisposableBag _subscriptions;

        private readonly GameplayCameraView _view;
        private readonly GameplaySettingsService _settings;
        private readonly GameplayConfig _config;

        public GameplayCameraPresenter(GameplayCameraView view, GameplaySettingsService settings, GameplayConfig config)
        {
            _view = view;
            _settings = settings;
            _config = config;
        }

        public void Start()
        {
            _settings.CameraDistance
                .Subscribe(distance => _view.SetDistanceScale(Mathf.Lerp(_config.NearestCameraDistanceScale, _config.FarthestCameraDistanceScale, distance)))
                .AddTo(ref _subscriptions);
        }

        public void Follow(Transform target)
        {
            _view.SetFollowTarget(target);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
