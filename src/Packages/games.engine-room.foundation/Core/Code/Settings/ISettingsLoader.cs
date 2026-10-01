using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Settings
{
    public interface ISettingsLoader
    {
        UniTask LoadAsync(CancellationToken ct);
    }
}
