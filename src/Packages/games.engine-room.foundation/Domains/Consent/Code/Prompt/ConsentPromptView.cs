using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Consent.Prompt
{
    internal sealed class ConsentPromptView : MonoBehaviour
    {
        public Observable<Unit> YesClicked => _yes.OnClickAsObservable();
        public Observable<Unit> NoClicked => _no.OnClickAsObservable();
        public Observable<Unit> CloseClicked => _close.OnClickAsObservable();
        public Observable<Unit> PolicyClicked => _policy.OnClickAsObservable();

        [SerializeField] private Button _yes = null!;
        [SerializeField] private Button _no = null!;
        [SerializeField] private Button _close = null!;
        [SerializeField] private Button _policy = null!;

        public void SetPolicyLinkVisible(bool isVisible)
        {
            _policy.gameObject.SetActive(isVisible);
        }

        public void SetInteractable(bool isInteractable)
        {
            _yes.interactable = isInteractable;
            _no.interactable = isInteractable;
            _close.interactable = isInteractable;
            _policy.interactable = isInteractable;
        }
    }
}
