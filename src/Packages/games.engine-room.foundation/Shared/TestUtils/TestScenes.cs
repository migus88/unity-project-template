#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestUtils
{
    public static class TestScenes
    {
        private static readonly TestOpenLedger Ledger = new();

        public static Scene Open(string path)
        {
            Ledger.Record(TestOpenContext.Current(), path);

            if (SceneManager.GetSceneByPath(path).isLoaded)
            {
                throw new InvalidOperationException($"{path} is already loaded: a previous test leaked it or it is open in the Editor. Close it and rerun.");
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        public static void Close(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static GameObject LoadPrefab(string path)
        {
            Ledger.Record(TestOpenContext.Current(), path);
            return PrefabUtility.LoadPrefabContents(path);
        }

        public static void UnloadPrefab(GameObject? root)
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
