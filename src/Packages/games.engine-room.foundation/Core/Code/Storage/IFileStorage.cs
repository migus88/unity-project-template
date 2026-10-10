using System.Threading;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Success = OneOf.Types.Success;

namespace Core.Storage
{
    public interface IFileStorage
    {
        bool Exists(string relativePath);
        UniTask<OneOf<string, NotFound, Error>> ReadAsync(string relativePath, CancellationToken ct);
        UniTask<OneOf<Success, Error>> WriteAsync(string relativePath, string content, CancellationToken ct);
        UniTask<OneOf<byte[], NotFound, Error>> ReadBytesAsync(string relativePath, CancellationToken ct);
        UniTask<OneOf<Success, Error>> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct);
        OneOf<Success, Error> Delete(string relativePath);
    }
}
