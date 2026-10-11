using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Environment
{
    [CreateAssetMenu(menuName = "Core/Build Environment")]
    public sealed class BuildEnvironment : ScriptableObject
    {
        [field: SerializeField] public string EnvironmentName { get; private set; } = "development";
        [field: SerializeField] public bool IsTelemetryAllowed { get; private set; }
        [field: SerializeField] public string TelemetryEndpoint { get; private set; } = string.Empty;
        [field: SerializeField] public string TelemetryIngestKey { get; private set; } = string.Empty;
        [field: SerializeField] public string CrashReportingDsn { get; private set; } = string.Empty;
        [field: SerializeField] public string CrashReportingEnvironment { get; private set; } = string.Empty;
        [field: SerializeField] public string PrivacyPolicyUrl { get; private set; } = string.Empty;
        [field: SerializeField] public int PrivacyPolicyVersion { get; private set; } = 1;

        public IReadOnlyList<RemoteConfigDefault> RemoteConfigDefaults => _remoteConfigDefaults;

        [SerializeField] private RemoteConfigDefault[] _remoteConfigDefaults = Array.Empty<RemoteConfigDefault>();

        private void OnValidate()
        {
            foreach (var problem in BuildEnvironmentValidation.Validate(this))
            {
                Debug.LogError($"Build environment '{name}': {problem}", this);
            }
        }
    }
}
