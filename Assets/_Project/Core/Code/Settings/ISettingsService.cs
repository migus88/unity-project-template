using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using R3;
using Success = OneOf.Types.Success;

namespace Core.Settings
{
    public interface ISettingsService
    {
        ReadOnlyReactiveProperty<SettingsState> Current { get; }

        void Apply(SettingsState state);
        UniTask<OneOf<Success, Error>> SaveAsync(CancellationToken ct);
    }
}
