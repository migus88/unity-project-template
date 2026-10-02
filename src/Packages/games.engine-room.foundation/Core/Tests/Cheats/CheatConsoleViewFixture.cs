using System;
using Core.Cheats;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Core.Tests.Cheats
{
    internal sealed class CheatConsoleViewFixture : IDisposable
    {
        public const int RowCount = 2;

        public CheatConsoleView View { get; }

        public TMP_InputField Input { get; }

        public TMP_Text Output { get; }

        public TMP_Text[] Labels { get; } = new TMP_Text[RowCount];

        public Image[] Highlights { get; } = new Image[RowCount];

        public TMP_Text More { get; }

        public GameObject Suggestions { get; }

        private readonly GameObject _root;

        public CheatConsoleViewFixture()
        {
            _root = new GameObject("CheatConsole");
            var panel = Child("Panel", _root.transform);
            Input = Child("Input", panel.transform).AddComponent<TMP_InputField>();
            Input.textComponent = Child("Text", Input.transform).AddComponent<TextMeshProUGUI>();
            Output = Child("Output", panel.transform).AddComponent<TextMeshProUGUI>();
            var scroll = Child("Scroll", panel.transform).AddComponent<ScrollRect>();
            Suggestions = Child("Suggestions", panel.transform);

            for (var i = 0; i < RowCount; i++)
            {
                var row = Child($"Row{i}", Suggestions.transform);
                Highlights[i] = row.AddComponent<Image>();
                Labels[i] = Child("Label", row.transform).AddComponent<TextMeshProUGUI>();
            }

            More = Child("More", Suggestions.transform).AddComponent<TextMeshProUGUI>();
            View = _root.AddComponent<CheatConsoleView>();

            var serialized = new SerializedObject(View);
            serialized.FindProperty("_panel").objectReferenceValue = panel;
            serialized.FindProperty("_input").objectReferenceValue = Input;
            serialized.FindProperty("_output").objectReferenceValue = Output;
            serialized.FindProperty("_outputScroll").objectReferenceValue = scroll;
            serialized.FindProperty("_suggestions").objectReferenceValue = Suggestions;
            var labels = serialized.FindProperty("_suggestionLabels");
            var highlights = serialized.FindProperty("_suggestionHighlights");
            labels.arraySize = RowCount;
            highlights.arraySize = RowCount;

            for (var i = 0; i < RowCount; i++)
            {
                labels.GetArrayElementAtIndex(i).objectReferenceValue = Labels[i];
                highlights.GetArrayElementAtIndex(i).objectReferenceValue = Highlights[i];
            }

            serialized.FindProperty("_moreSuggestions").objectReferenceValue = More;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_root);
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
