namespace Core.RemoteConfig
{
    public static class CoreFlags
    {
        public static readonly BoolFlag TelemetryEnabled = new("telemetryEnabled", true);
        public static readonly BoolFlag RecordingUploadEnabled = new("recordingUploadEnabled", true);
        public static readonly IntFlag PrivacyPolicyVersion = new("privacyPolicyVersion", 0);
    }
}
