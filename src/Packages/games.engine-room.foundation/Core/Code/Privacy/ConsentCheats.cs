#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using Core.Cheats;
using Cysharp.Threading.Tasks;

namespace Core.Privacy
{
    public sealed class ConsentCheats : ICheatProvider
    {
        private const string ActionParameter = "action";
        private const string PolicyVersionParameter = "policyVersion";
        private const string ClearAction = "clear";
        private const string ShowAction = "show";
        private const string GrantAction = "grant";
        private const string DenyAction = "deny";
        private const string None = "none";
        private const int FirstPolicyVersion = 1;

        private static readonly string[] Actions = { ClearAction, ShowAction, GrantAction, DenyAction };

        public IReadOnlyList<ICheat> Cheats { get; }

        private readonly ConsentService _consent;

        public ConsentCheats(ConsentService consent)
        {
            _consent = consent;
            Cheats = new ICheat[]
            {
                new CheatCommand(
                    "consent",
                    "Shows, clears, grants or denies the telemetry consent; grant and deny default to the stored policy version.",
                    new[] { CheatParameter.Choice(ActionParameter, () => Actions), CheatParameter.Int(PolicyVersionParameter).Optional() },
                    ExecuteAsync),
            };
        }

        private async UniTask<string> ExecuteAsync(CheatArguments arguments, CancellationToken ct)
        {
            var action = arguments.GetText(ActionParameter).ToLowerInvariant();
            var policyVersion = arguments.GetInt(PolicyVersionParameter, _consent.Current.CurrentValue.PolicyVersion ?? FirstPolicyVersion);

            switch (action)
            {
                case ShowAction:
                    return Show();
                case ClearAction:
                    await _consent.ClearAsync(ct);
                    return "Consent cleared; the prompt shows on the next check.";
                case GrantAction:
                    await _consent.GrantAsync(policyVersion, ct);
                    return $"Consent granted for policy version {policyVersion}.";
                case DenyAction:
                    await _consent.DenyAsync(policyVersion, ct);
                    return $"Consent denied for policy version {policyVersion}.";
                default:
                    throw new InvalidOperationException($"Unknown consent action '{action}'.");
            }
        }

        private string Show()
        {
            var snapshot = _consent.Current.CurrentValue;
            return $"state\t{snapshot.State}\npolicy version\t{snapshot.PolicyVersion?.ToString() ?? None}\ninstall id\t{snapshot.InstallId ?? None}\nsession id\t{_consent.SessionId}";
        }
    }
}
#endif
