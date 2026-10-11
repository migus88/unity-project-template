using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Core.Privacy
{
    public interface IConsentService
    {
        ReadOnlyReactiveProperty<ConsentSnapshot> Current { get; }

        bool IsGranted { get; }

        string? InstallId { get; }

        string SessionId { get; }

        bool NeedsPrompt(int currentPolicyVersion);
        UniTask GrantAsync(int policyVersion, CancellationToken ct);
        UniTask DenyAsync(int policyVersion, CancellationToken ct);
    }
}
