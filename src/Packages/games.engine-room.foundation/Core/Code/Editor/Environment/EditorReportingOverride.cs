using System;
using Core.Environment;
using Core.Logging;
using UnityEditor;

namespace Core.Editor.Environment
{
    [InitializeOnLoad]
    public static class EditorReportingOverride
    {
        public static bool IsEnabled => SessionState.GetBool(ReportingRule.EditorOverrideKey, false);

        static EditorReportingOverride()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void SetEnabled(bool isEnabled)
        {
            if (isEnabled && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("The Editor reporting override can only be armed in Edit mode.");
            }

            if (isEnabled)
            {
                SessionState.SetBool(ReportingRule.EditorOverrideKey, true);
            }
            else
            {
                SessionState.EraseBool(ReportingRule.EditorOverrideKey);
            }
        }

        internal static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(ReportingRule.EditorOverrideKey);
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode && IsEnabled)
            {
                Log.Info(LogTags.Environment, "Telemetry and crash reporting are enabled for this Play session (Editor override).");
            }
        }
    }
}
