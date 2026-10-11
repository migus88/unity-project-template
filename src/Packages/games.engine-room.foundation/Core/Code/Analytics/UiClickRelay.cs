using R3;
using UnityEngine;

namespace Core.Analytics
{
    [DisallowMultipleComponent]
    public sealed class UiClickRelay : MonoBehaviour
    {
        [field: SerializeField]
        public string Screen { get; private set; } = "";

        public Observable<UiClick> Clicked => _clicked;

        private readonly Subject<UiClick> _clicked = new();

        public static void Emit(Component source, string element)
        {
            if (string.IsNullOrEmpty(element))
            {
                return;
            }

            var relay = source.GetComponentInParent<UiClickRelay>(true);
            if (relay != null)
            {
                relay.Request(element);
            }
        }

        public void Request(string element)
        {
            _clicked.OnNext(new UiClick(Screen, element));
        }

        private void OnDestroy()
        {
            _clicked.Dispose();
        }
    }
}
