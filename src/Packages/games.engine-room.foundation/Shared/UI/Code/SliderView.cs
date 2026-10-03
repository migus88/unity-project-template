using Core.Audio;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    public sealed class SliderView : MonoBehaviour
    {
        public Observable<float> ValueChanged => _slider.onValueChanged.AsObservable(destroyCancellationToken);

        [SerializeField] private Slider _slider = null!;
        [SerializeField] private TMP_Text _valueLabel = null!;
        [SerializeField] private UiInteraction _changeSound = UiInteraction.Tick;

        public void SetValue(float value)
        {
            _slider.SetValueWithoutNotify(value);
            ShowValue(_slider.value);
        }

        public void SetInteractable(bool isInteractable)
        {
            _slider.interactable = isInteractable;
        }

        private void Awake()
        {
            _slider.onValueChanged.AddListener(OnValueChanged);
            ShowValue(_slider.value);
        }

        private void OnValueChanged(float value)
        {
            ShowValue(value);
            UiInteractionRelay.Emit(this, _changeSound);
        }

        private void ShowValue(float value)
        {
            _valueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
