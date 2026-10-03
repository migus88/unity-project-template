using System.Collections.Generic;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Cheats
{
    public sealed class CheatConsoleView : MonoBehaviour
    {
        private const int CaretPendingFrames = 2;

        public Observable<string> Submitted => _submitted;
        public Observable<string> Entered => _input.onSubmit.AsObservable();
        public Observable<string> LineChanged => _input.onValueChanged.AsObservable();
        public string Line => _input.text;
        public string LastReply { get; set; } = string.Empty;
        public bool IsShown => _panel.activeSelf;

        [SerializeField] private GameObject _panel = null!;
        [SerializeField] private TMP_InputField _input = null!;
        [SerializeField] private TMP_Text _output = null!;
        [SerializeField] private ScrollRect _outputScroll = null!;
        [SerializeField] private GameObject _suggestions = null!;
        [SerializeField] private TMP_Text[] _suggestionLabels = null!;
        [SerializeField] private Image[] _suggestionHighlights = null!;
        [SerializeField] private TMP_Text _moreSuggestions = null!;

        private int _caretPendingFrames;
        private bool _isFocusPending;
        private bool _isScrollPending;

        private readonly Subject<string> _submitted = new();

        public void Submit(string line)
        {
            _submitted.OnNext(line);
        }

        public void Show()
        {
            _panel.SetActive(true);
            _input.SetTextWithoutNotify(string.Empty);
            _input.ActivateInputField();
            _isScrollPending = true;
        }

        public void Hide()
        {
            _input.DeactivateInputField();
            _panel.SetActive(false);
        }

        public void SetLine(string line)
        {
            _input.SetTextWithoutNotify(line);
            MoveCaretToEnd();
            KeepCaretAtEnd();
        }

        public void KeepCaretAtEnd()
        {
            _caretPendingFrames = CaretPendingFrames;
        }

        public void Focus()
        {
            if (_panel.activeSelf)
            {
                _input.ActivateInputField();
                _isFocusPending = true;
            }
        }

        public void ShowOutput(string text)
        {
            _output.text = text;
            _isScrollPending = true;
        }

        public void ShowSuggestions(IReadOnlyList<string> values, int selected)
        {
            var rows = _suggestionLabels.Length;
            var shown = Mathf.Min(values.Count, rows);
            var first = Mathf.Clamp(selected - rows + 1, 0, Mathf.Max(0, values.Count - rows));

            for (var i = 0; i < rows; i++)
            {
                var isShown = i < shown;
                _suggestionLabels[i].transform.parent.gameObject.SetActive(isShown);

                if (isShown)
                {
                    _suggestionLabels[i].text = values[first + i];
                    _suggestionHighlights[i].enabled = first + i == selected;
                }
            }

            var hidden = values.Count - shown;
            _moreSuggestions.gameObject.SetActive(hidden > 0);
            _moreSuggestions.text = hidden > 0 ? $"+{hidden} more" : string.Empty;
            _suggestions.SetActive(values.Count > 0);
        }

        private void Awake()
        {
            _input.onValidateInput = RejectConsoleKeys;
            _input.restoreOriginalTextOnEscape = false;
            _input.onFocusSelectAll = false;
        }

        private void LateUpdate()
        {
            if (_isFocusPending)
            {
                _isFocusPending = false;

                if (_panel.activeSelf)
                {
                    _input.ActivateInputField();
                    KeepCaretAtEnd();
                }
            }

            if (_caretPendingFrames > 0)
            {
                _caretPendingFrames--;
                MoveCaretToEnd();
            }

            if (_isScrollPending)
            {
                _isScrollPending = false;
                _outputScroll.verticalNormalizedPosition = 0f;
            }
        }

        private void MoveCaretToEnd()
        {
            var end = _input.text.Length;
            _input.caretPosition = end;
            _input.stringPosition = end;
            _input.selectionAnchorPosition = end;
            _input.selectionFocusPosition = end;
        }

        private static char RejectConsoleKeys(string text, int index, char character)
        {
            return character is '`' or '\t' ? '\0' : character;
        }

        private void OnDestroy()
        {
            _submitted.Dispose();
        }
    }
}
