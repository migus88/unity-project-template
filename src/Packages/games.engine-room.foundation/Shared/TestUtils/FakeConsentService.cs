using System.Collections.Generic;
using System.Threading;
using Core.Privacy;
using Cysharp.Threading.Tasks;
using R3;

namespace TestUtils
{
    public sealed class FakeConsentService : IConsentService
    {
        public ReadOnlyReactiveProperty<ConsentSnapshot> Current => _current;

        public bool IsGranted => _current.CurrentValue.State == ConsentState.Granted;

        public string? InstallId => _current.CurrentValue.InstallId;

        public string SessionId => "test-session";

        public IReadOnlyList<int> GrantedVersions => _grantedVersions;

        public IReadOnlyList<int> DeniedVersions => _deniedVersions;

        private int _nextId;

        private readonly ReactiveProperty<ConsentSnapshot> _current;
        private readonly List<int> _grantedVersions = new();
        private readonly List<int> _deniedVersions = new();

        public FakeConsentService(ConsentSnapshot initial)
        {
            _current = new ReactiveProperty<ConsentSnapshot>(initial);
        }

        public bool NeedsPrompt(int currentPolicyVersion)
        {
            var snapshot = _current.CurrentValue;
            return snapshot.State == ConsentState.Unknown || (snapshot.State == ConsentState.Granted && snapshot.PolicyVersion < currentPolicyVersion);
        }

        public UniTask GrantAsync(int policyVersion, CancellationToken ct)
        {
            _grantedVersions.Add(policyVersion);
            var installId = _current.CurrentValue.InstallId ?? NewId();
            _current.Value = new ConsentSnapshot(ConsentState.Granted, policyVersion, installId);
            return UniTask.CompletedTask;
        }

        public UniTask DenyAsync(int policyVersion, CancellationToken ct)
        {
            _deniedVersions.Add(policyVersion);
            var current = _current.CurrentValue;
            var installId = current.State == ConsentState.Granted ? NewId() : current.InstallId;
            _current.Value = new ConsentSnapshot(ConsentState.Denied, policyVersion, installId);
            return UniTask.CompletedTask;
        }

        private string NewId()
        {
            _nextId++;
            return $"generated-id-{_nextId}";
        }
    }
}
