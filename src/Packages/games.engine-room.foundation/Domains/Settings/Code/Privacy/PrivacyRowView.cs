using R3;
using Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Settings.Privacy
{
    internal sealed class PrivacyRowView : MonoBehaviour
    {
        public Observable<Unit> ConsentToggled => _consent.PreviousClicked.Merge(_consent.NextClicked);
        public Observable<Unit> PolicyClicked => _policyButton.OnClickAsObservable();

        [SerializeField] private SelectorView _consent = null!;
        [SerializeField] private Button _policyButton = null!;
        [SerializeField] private TMP_InputField _installId = null!;
        [SerializeField] private GameObject _deletionHint = null!;

        public void SetConsent(string valueText)
        {
            _consent.SetValue(valueText);
        }

        public void SetInstallId(string text, bool hasId)
        {
            _installId.SetTextWithoutNotify(text);
            _deletionHint.SetActive(hasId);
        }

        public void SetPolicyLinkVisible(bool isVisible)
        {
            _policyButton.gameObject.SetActive(isVisible);
        }

        public void SetInteractable(bool isInteractable)
        {
            _consent.SetInteractable(isInteractable);
            _policyButton.interactable = isInteractable;
        }
    }
}
