using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Success = OneOf.Types.Success;

namespace Core.Save
{
    public interface ISaveStore
    {
        int ActiveSlot { get; }
        UniTask<OneOf<Success, Error>> SelectSlotAsync(int slot, CancellationToken ct);
        OneOf<T, NotFound, Corrupted> Read<T>(SaveSection<T> section) where T : class;
        void Write<T>(SaveSection<T> section, T data) where T : class;
        UniTask<OneOf<Success, Error>> FlushAsync(CancellationToken ct);
        UniTask<OneOf<Success, Error>> DeleteSlotAsync(int slot, CancellationToken ct);
    }
}
