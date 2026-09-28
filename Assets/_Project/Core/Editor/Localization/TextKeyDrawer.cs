using Core.Localization;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Localization
{
    [CustomPropertyDrawer(typeof(TextKey))]
    internal sealed class TextKeyDrawer : PropertyDrawer
    {
        private const string TableFieldName = "_table";
        private const string KeyFieldName = "_key";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var current = new TextKey(property.FindPropertyRelative(TableFieldName).stringValue, property.FindPropertyRelative(KeyFieldName).stringValue);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldPosition = EditorGUI.PrefixLabel(position, label);
                var caption = string.IsNullOrEmpty(current.Key) ? "None" : current.ToString();

                if (EditorGUI.DropdownButton(fieldPosition, new GUIContent(caption), FocusType.Keyboard))
                {
                    ShowKeyMenu(property.serializedObject, property.propertyPath, current);
                }
            }
        }

        private static void ShowKeyMenu(SerializedObject serializedObject, string propertyPath, TextKey current)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(current.Key), () => Assign(serializedObject, propertyPath, default));

            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(LocalizationTable)}"))
            {
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(AssetDatabase.GUIDToAssetPath(guid));

                foreach (var entry in table.Entries)
                {
                    var key = new TextKey(table.TableName, entry.Key);
                    menu.AddItem(new GUIContent(key.ToString()), key.Equals(current), () => Assign(serializedObject, propertyPath, key));
                }
            }

            menu.ShowAsContext();
        }

        private static void Assign(SerializedObject serializedObject, string propertyPath, TextKey key)
        {
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            property.FindPropertyRelative(TableFieldName).stringValue = key.Table;
            property.FindPropertyRelative(KeyFieldName).stringValue = key.Key;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
