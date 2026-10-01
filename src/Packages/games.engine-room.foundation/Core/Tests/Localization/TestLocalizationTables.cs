using Core.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Core.Tests.Localization
{
    internal static class TestLocalizationTables
    {
        public static LocalizationTable Create(string tableName, params (string Key, string English, string Polish)[] entries)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            var serializedTable = new SerializedObject(table);
            serializedTable.FindProperty("<TableName>k__BackingField").stringValue = tableName;
            var entriesProperty = serializedTable.FindProperty("_entries");
            entriesProperty.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                entryProperty.FindPropertyRelative("_key").stringValue = entries[i].Key;
                entryProperty.FindPropertyRelative("_english").stringValue = entries[i].English;
                entryProperty.FindPropertyRelative("_polish").stringValue = entries[i].Polish;
            }

            serializedTable.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        public static LocalizedLabel CreateLabel(Transform parent, TextKey key)
        {
            var gameObject = new GameObject("Label");
            gameObject.SetActive(false);
            gameObject.transform.SetParent(parent);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            var label = gameObject.AddComponent<LocalizedLabel>();
            var serializedLabel = new SerializedObject(label);
            serializedLabel.FindProperty("_key._table").stringValue = key.Table;
            serializedLabel.FindProperty("_key._key").stringValue = key.Key;
            serializedLabel.FindProperty("_text").objectReferenceValue = text;
            serializedLabel.ApplyModifiedPropertiesWithoutUndo();
            return label;
        }
    }
}
