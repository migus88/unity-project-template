#if UNITY_EDITOR
using Core.Domains;
using UnityEditor;

namespace Bootstrap
{
    public static class EditorScopeScene
    {
        public static bool IsScopeSceneOf(DomainDescriptor descriptor, string scenePath)
        {
            var content = descriptor.EditorContent;

            if (content == null || !content.ScopeScene.IsValid || string.IsNullOrEmpty(scenePath))
            {
                return false;
            }

            var sceneAsset = LoadableSceneIdEditorUtility.LoadableSceneIdToScene(content.ScopeScene);
            return sceneAsset != null && AssetDatabase.GetAssetPath(sceneAsset) == scenePath;
        }
    }
}
#endif
