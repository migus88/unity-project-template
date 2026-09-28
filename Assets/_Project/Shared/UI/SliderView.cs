using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    public sealed class SliderView : MonoBehaviour
    {
        [SerializeField, Required] private Slider _slider = null!;
        [SerializeField, Required] private TMP_Text _valueLabel = null!;

        public Observable<float> ValueChanged => _slider.onValueChanged.AsObservable(destroyCancellationToken);

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
            _slider.onValueChanged.AddListener(ShowValue);
            ShowValue(_slider.value);
        }

        private void ShowValue(float value)
        {
            _valueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
