using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    internal sealed class MainMenuView : MonoBehaviour
    {
        public Observable<Unit> PlayClicked => _playButton.OnClickAsObservable();
        public Observable<Unit> SettingsClicked => _settingsButton.OnClickAsObservable();
        public Observable<Unit> QuitClicked => _quitButton.OnClickAsObservable();

        [SerializeField] private Button _playButton = null!;
        [SerializeField] private Button _settingsButton = null!;
        [SerializeField] private Button _quitButton = null!;
        [SerializeField] private TMP_Text _versionLabel = null!;

        public void SetVersion(string version)
        {
            _versionLabel.text = version;
        }

        public void SetInteractable(bool isInteractable)
        {
            _playButton.interactable = isInteractable;
            _settingsButton.interactable = isInteractable;
            _quitButton.interactable = isInteractable;
        }
    }
}
