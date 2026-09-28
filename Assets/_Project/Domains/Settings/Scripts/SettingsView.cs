using System.Collections.Generic;
using R3;
using Shared.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Settings
{
    internal sealed class SettingsView : MonoBehaviour
    {
        [SerializeField, Required] private SliderView _masterVolume = null!;
        [SerializeField, Required] private SliderView _musicVolume = null!;
        [SerializeField, Required] private SliderView _sfxVolume = null!;
        [SerializeField, Required] private SliderView _uiVolume = null!;
        [SerializeField, Required] private DropdownView _language = null!;
        [SerializeField, Required] private Button _backButton = null!;

        public Observable<float> MasterVolumeChanged => _masterVolume.ValueChanged;
        public Observable<float> MusicVolumeChanged => _musicVolume.ValueChanged;
        public Observable<float> SfxVolumeChanged => _sfxVolume.ValueChanged;
        public Observable<float> UiVolumeChanged => _uiVolume.ValueChanged;
        public Observable<int> LanguageIndexChanged => _language.SelectedIndexChanged;
        public Observable<Unit> BackClicked => _backButton.OnClickAsObservable();

        public void SetVolumes(float master, float music, float sfx, float ui)
        {
            _masterVolume.SetValue(master);
            _musicVolume.SetValue(music);
            _sfxVolume.SetValue(sfx);
            _uiVolume.SetValue(ui);
        }

        public void SetLanguages(IReadOnlyList<string> languageNames, int selectedIndex)
        {
            _language.SetOptions(languageNames, selectedIndex);
        }

        public void SetInteractable(bool isInteractable)
        {
            _masterVolume.SetInteractable(isInteractable);
            _musicVolume.SetInteractable(isInteractable);
            _sfxVolume.SetInteractable(isInteractable);
            _uiVolume.SetInteractable(isInteractable);
            _language.SetInteractable(isInteractable);
            _backButton.interactable = isInteractable;
        }
    }
}
