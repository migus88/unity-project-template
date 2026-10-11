using System;
using System.Threading;
using Core.Logging;
using Core.Settings;
using Cysharp.Threading.Tasks;
using R3;

namespace Core.Privacy
{
    public sealed class ConsentService : IConsentService, IDisposable
    {
        public ReadOnlyReactiveProperty<ConsentSnapshot> Current => _current;

        public bool IsGranted => _current.CurrentValue.State == ConsentState.Granted;

        public string? InstallId => _current.CurrentValue.InstallId;

        public string SessionId { get; }

        private readonly ISettingsService _settings;
        private readonly ReactiveProperty<ConsentSnapshot> _current;

        public ConsentService(ISettingsService settings)
        {
            _settings = settings;
            _current = new ReactiveProperty<ConsentSnapshot>(new ConsentSnapshot(ConsentState.Unknown, null, null));
            SessionId = NewId();
        }

        public bool NeedsPrompt(int currentPolicyVersion)
        {
            var snapshot = _current.CurrentValue;

            return snapshot.State switch
            {
                ConsentState.Unknown => true,
                ConsentState.Granted => snapshot.PolicyVersion < currentPolicyVersion,
                _ => false,
            };
        }

        public UniTask GrantAsync(int policyVersion, CancellationToken ct)
        {
            var installId = _current.CurrentValue.InstallId ?? NewId();
            return ChangeAsync(new ConsentSnapshot(ConsentState.Granted, policyVersion, installId), ct);
        }

        public UniTask DenyAsync(int policyVersion, CancellationToken ct)
        {
            var current = _current.CurrentValue;
            var installId = current.State == ConsentState.Granted ? NewId() : current.InstallId;
            return ChangeAsync(new ConsentSnapshot(ConsentState.Denied, policyVersion, installId), ct);
        }

        internal async UniTask LoadAsync(CancellationToken ct)
        {
            var dto = _settings.Read(PrivacySettings.Section);
            var snapshot = ToSnapshot(dto);
            _current.Value = snapshot;

            if (snapshot.State == ConsentState.Granted && dto.InstallId == null)
            {
                await SaveAsync(snapshot, ct);
            }
        }

        internal UniTask ClearAsync(CancellationToken ct)
        {
            return ChangeAsync(new ConsentSnapshot(ConsentState.Unknown, null, null), ct);
        }

        private async UniTask ChangeAsync(ConsentSnapshot snapshot, CancellationToken ct)
        {
            _current.Value = snapshot;
            await SaveAsync(snapshot, ct);
        }

        private async UniTask SaveAsync(ConsentSnapshot snapshot, CancellationToken ct)
        {
            _settings.Write(PrivacySettings.Section, ToDto(snapshot));
            var saved = await _settings.SaveAsync(ct);

            if (saved.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.Privacy, $"Consent could not be saved, keeping it for this session: {error.Message}");
            }
        }

        private static ConsentSnapshot ToSnapshot(PrivacySettingsDto dto)
        {
            var state = ParseConsent(dto.Consent);

            if (state == ConsentState.Unknown)
            {
                return new ConsentSnapshot(ConsentState.Unknown, null, null);
            }

            if (state == ConsentState.Granted && dto.PolicyVersion is not > 0)
            {
                Log.Warn(LogTags.Privacy, "Stored consent has no valid policy version; asking again.");
                return new ConsentSnapshot(ConsentState.Unknown, null, null);
            }

            var installId = state == ConsentState.Granted ? dto.InstallId ?? NewId() : dto.InstallId;
            return new ConsentSnapshot(state, dto.PolicyVersion, installId);
        }

        private static ConsentState ParseConsent(string? consent)
        {
            switch (consent)
            {
                case null:
                    return ConsentState.Unknown;
                case PrivacySettings.GrantedValue:
                    return ConsentState.Granted;
                case PrivacySettings.DeniedValue:
                    return ConsentState.Denied;
                default:
                    Log.Warn(LogTags.Privacy, $"Stored consent '{consent}' is not recognised; treating it as unknown.");
                    return ConsentState.Unknown;
            }
        }

        private static PrivacySettingsDto ToDto(ConsentSnapshot snapshot)
        {
            var consent = snapshot.State switch
            {
                ConsentState.Granted => PrivacySettings.GrantedValue,
                ConsentState.Denied => PrivacySettings.DeniedValue,
                _ => null,
            };

            return new PrivacySettingsDto(consent, snapshot.PolicyVersion, snapshot.InstallId);
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString();
        }

        public void Dispose()
        {
            _current.Dispose();
        }
    }
}
