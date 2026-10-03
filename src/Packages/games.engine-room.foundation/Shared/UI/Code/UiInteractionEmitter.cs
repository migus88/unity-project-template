using Core.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shared.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiInteractionEmitter : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private UiInteraction _hover = UiInteraction.Hover;
        [SerializeField] private UiInteraction _click = UiInteraction.Click;

        private Selectable _selectable = null!;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable.IsInteractable())
            {
                UiInteractionRelay.Emit(this, _hover);
            }
        }

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (_selectable is Button button)
            {
                button.onClick.AddListener(EmitClick);
            }
        }

        private void EmitClick()
        {
            UiInteractionRelay.Emit(this, _click);
        }

        private void OnDestroy()
        {
            if (_selectable is Button button)
            {
                button.onClick.RemoveListener(EmitClick);
            }
        }
    }
}
