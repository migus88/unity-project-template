using Core.Environment;
using UnityEditor;

namespace Core.Editor.Environment
{
    internal static class BuildEnvironmentSlot
    {
        private const string EnvironmentProperty = "<Environment>k__BackingField";

        public static bool Assign(ActiveBuildEnvironment slot, BuildEnvironment environment)
        {
            if (slot.Environment == environment)
            {
                return false;
            }

            using var serialized = new SerializedObject(slot);
            serialized.FindProperty(EnvironmentProperty).objectReferenceValue = environment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        public static bool Restore(BuildEnvironmentMap map)
        {
            if (map.Active == null || map.EditorEnvironment == null)
            {
                return false;
            }

            return Assign(map.Active, map.EditorEnvironment);
        }
    }
}
