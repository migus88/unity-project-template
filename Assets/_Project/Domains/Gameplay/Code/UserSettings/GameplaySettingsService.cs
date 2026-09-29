using System;
using System.Threading;
using Core.Logging;
using Core.Results;
using Core.Settings;
using Cysharp.Threading.Tasks;
using OneOf;
using R3;
using Success = OneOf.Types.Success;

namespace Gameplay.UserSettings
{
    internal sealed class GameplaySettingsService : IDisposable
    {
        public ReadOnlyReactiveProperty<float> CameraDistance => _cameraDistance;

        private int _changeCount;
        private int _savedChangeCount;

        private readonly ISettingsService _settings;
        private readonly ReactiveProperty<float> _cameraDistance;

        public GameplaySettingsService(ISettingsService settings)
        {
            _settings = settings;
            _cameraDistance = new ReactiveProperty<float>(ReadCameraDistance());
        }

        public void SetCameraDistance(float cameraDistance)
        {
            if (!IsValidCameraDistance(cameraDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(cameraDistance), cameraDistance, "Camera distance must be between 0 and 1.");
            }

            if (cameraDistance == _cameraDistance.Value)
            {
                return;
            }

            _cameraDistance.Value = cameraDistance;
            _settings.Write(GameplaySettings.Section, new GameplaySettingsDto(cameraDistance));
            _changeCount++;
        }

        public async UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct)
        {
            var changeCount = _changeCount;

            if (changeCount == _savedChangeCount)
            {
                return new Success();
            }

            var saved = await _settings.SaveAsync(ct);

            if (saved.IsT0)
            {
                _savedChangeCount = changeCount;
            }

            return saved;
        }

        private float ReadCameraDistance()
        {
            var stored = _settings.Read(GameplaySettings.Section).CameraDistance;

            if (stored is not { } cameraDistance)
            {
                Log.Warn(LogTags.Gameplay, "Stored camera distance is missing, using the default.");
                return GameplaySettings.DefaultCameraDistance;
            }

            if (!IsValidCameraDistance(cameraDistance))
            {
                Log.Warn(LogTags.Gameplay, $"Stored camera distance {cameraDistance} is out of range, using the default.");
                return GameplaySettings.DefaultCameraDistance;
            }

            return cameraDistance;
        }

        private static bool IsValidCameraDistance(float cameraDistance)
        {
            return cameraDistance is >= 0f and <= 1f;
        }

        public void Dispose()
        {
            _cameraDistance.Dispose();
        }
    }
}
