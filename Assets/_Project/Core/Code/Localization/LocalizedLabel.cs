using TMPro;
using UnityEngine;

namespace Core.Localization
{
    public sealed class LocalizedLabel : MonoBehaviour
    {
        public TextKey Key => _key;

        [SerializeField] private TextKey _key;
        [SerializeField] private TMP_Text _text = null!;

        public void SetText(string text)
        {
            _text.text = text;
        }

        private void OnValidate()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
        }
    }
}
