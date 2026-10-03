using R3;
using UnityEngine;

namespace Core.Audio
{
    [DisallowMultipleComponent]
    public sealed class UiInteractionRelay : MonoBehaviour
    {
        public Observable<UiInteraction> Requested => _requested;

        private readonly Subject<UiInteraction> _requested = new();

        public static void Emit(Component source, UiInteraction interaction)
        {
            if (interaction == UiInteraction.None)
            {
                return;
            }

            var relay = source.GetComponentInParent<UiInteractionRelay>(true);
            if (relay != null)
            {
                relay.Request(interaction);
            }
        }

        public void Request(UiInteraction interaction)
        {
            _requested.OnNext(interaction);
        }

        private void OnDestroy()
        {
            _requested.Dispose();
        }
    }
}
