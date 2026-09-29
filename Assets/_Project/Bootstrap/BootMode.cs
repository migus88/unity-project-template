using UnityEngine;

namespace Bootstrap
{
    public static class BootMode
    {
#if UNITY_EDITOR
        public const string DebugScopeScenePathKey = "Bootstrap.DebugScopeScenePath";
        public const string TestKey = "Bootstrap.Test";
#endif

        public static Kind Current { get; private set; } = Kind.Normal;

#if UNITY_EDITOR
        public static string DebugScopeScenePath { get; private set; } = string.Empty;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Resolve()
        {
#if UNITY_EDITOR
            DebugScopeScenePath = UnityEditor.SessionState.GetString(DebugScopeScenePathKey, string.Empty);
            Current = ResolveEditorKind();
#else
            Current = Kind.Normal;
#endif
        }

#if UNITY_EDITOR
        private static Kind ResolveEditorKind()
        {
            if (UnityEditor.SessionState.GetBool(TestKey, false))
            {
                return Kind.Test;
            }

            return DebugScopeScenePath.Length > 0 ? Kind.DebugDomain : Kind.Normal;
        }
#endif

        public enum Kind
        {
            None = 0,
            Normal = 1,
            DebugDomain = 2,
            Test = 3,
        }
    }
}
