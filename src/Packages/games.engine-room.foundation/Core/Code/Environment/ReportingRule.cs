namespace Core.Environment
{
    public sealed class ReportingRule
    {
        public const string EditorOverrideKey = "Core.Environment.EditorReportingOverride";

        public bool IsReportingAllowed => !_isEditor || _editorOverride.IsEnabled;

        public bool IsEditorOverrideActive => _isEditor && _editorOverride.IsEnabled;

        private readonly bool _isEditor;
        private readonly IReportingOverride _editorOverride;

        public ReportingRule(bool isEditor, IReportingOverride editorOverride)
        {
            _isEditor = isEditor;
            _editorOverride = editorOverride;
        }
    }
}
