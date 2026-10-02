using UnityEngine;

namespace Bootstrap
{
    public sealed class BootCoverView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas = null!;

        public bool IsShown => _canvas.enabled;

        public void Hide()
        {
            _canvas.enabled = false;
        }
    }
}
