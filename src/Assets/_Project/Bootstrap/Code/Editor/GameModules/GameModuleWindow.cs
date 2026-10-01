using UnityEditor;
using UnityEngine;

namespace Bootstrap.Editor.GameModules
{
    internal sealed class GameModuleWindow : EditorWindow
    {
        private const string Title = "Create Game Module";

        private string _name = "Game";
        private string _error = string.Empty;

        [MenuItem("Tools/Foundation/Create Game Module")]
        private static void Open()
        {
            var window = GetWindow<GameModuleWindow>(true, Title);
            window.minSize = new Vector2(360, 140);
            window.ShowUtility();
        }

        [MenuItem("Tools/Foundation/Create Game Module", isValidateFunction: true)]
        private static bool CanOpen()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox($"Creates '{GameModuleScaffold.ProjectFolder}/<Name>/' with a GameModule asset, an IMainFlow stub, a variant of the root scope prefab and VContainerSettings, and makes it the game that boots.", MessageType.Info);
            _name = EditorGUILayout.TextField("Name", _name);

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }

            if (GUILayout.Button("Create"))
            {
                GameModuleScaffold.Create(_name).Switch(
                    _ => Close(),
                    error => _error = error.Message);
            }
        }
    }
}
