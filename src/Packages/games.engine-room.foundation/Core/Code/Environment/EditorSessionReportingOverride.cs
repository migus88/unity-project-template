namespace Core.Environment
{
    internal sealed class EditorSessionReportingOverride : IReportingOverride
    {
        public bool IsEnabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool(ReportingRule.EditorOverrideKey, false);
#else
                return false;
#endif
            }
        }
    }
}
