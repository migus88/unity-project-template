using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    public sealed class SelectorView : MonoBehaviour
    {
        public Observable<Unit> PreviousClicked => _previousButton.OnClickAsObservable();
        public Observable<Unit> NextClicked => _nextButton.OnClickAsObservable();

        [SerializeField] private Button _previousButton = null!;
        [SerializeField] private Button _nextButton = null!;
        [SerializeField] private TMP_Text _valueLabel = null!;

        public void SetValue(string value)
        {
            _valueLabel.text = value;
        }

        public void SetInteractable(bool isInteractable)
        {
            _previousButton.interactable = isInteractable;
            _nextButton.interactable = isInteractable;
        }
    }
}
