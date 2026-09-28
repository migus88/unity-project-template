using UnityEngine;

namespace Bootstrap
{
    public static class BootMode
    {
#if UNITY_EDITOR
        public const string DebugScopeScenePathKey = "Bootstrap.DebugScopeScenePath";
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
            Current = DebugScopeScenePath.Length > 0 ? Kind.DebugDomain : Kind.Normal;
#else
            Current = Kind.Normal;
#endif
        }

        public enum Kind
        {
            None = 0,
            Normal = 1,
            DebugDomain = 2,
        }
    }
}
