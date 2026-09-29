using R3;
using Shared.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Pause
{
    internal sealed class PauseView : MonoBehaviour
    {
        public Observable<Unit> ResumeClicked => _resumeButton.OnClickAsObservable();
        public Observable<Unit> SettingsClicked => _settingsButton.OnClickAsObservable();
        public Observable<Unit> QuitToMenuClicked => _quitToMenuButton.OnClickAsObservable();
        public Observable<float> CameraDistanceChanged => _cameraDistance.ValueChanged;

        [SerializeField, Required] private Button _resumeButton = null!;
        [SerializeField, Required] private Button _settingsButton = null!;
        [SerializeField, Required] private Button _quitToMenuButton = null!;
        [SerializeField, Required] private SliderView _cameraDistance = null!;

        public void SetCameraDistance(float cameraDistance)
        {
            _cameraDistance.SetValue(cameraDistance);
        }

        public void SetInteractable(bool isInteractable)
        {
            _resumeButton.interactable = isInteractable;
            _settingsButton.interactable = isInteractable;
            _quitToMenuButton.interactable = isInteractable;
            _cameraDistance.SetInteractable(isInteractable);
        }
    }
}
