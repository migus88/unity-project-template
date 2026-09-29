using System.Collections.Generic;
using System.Threading;
using Core.Results;
using Core.Storage;
using Cysharp.Threading.Tasks;
using OneOf;
using Success = OneOf.Types.Success;

namespace TestUtils
{
    public sealed class InMemoryFileStorage : IFileStorage
    {
        public Dictionary<string, string> Files { get; } = new();
        public List<string> WrittenPaths { get; } = new();

        public bool Exists(string relativePath)
        {
            return Files.ContainsKey(relativePath);
        }

        public UniTask<OneOf<string, NotFound, Error>> ReadAsync(string relativePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (Files.TryGetValue(relativePath, out var content))
            {
                return UniTask.FromResult<OneOf<string, NotFound, Error>>(content);
            }

            return UniTask.FromResult<OneOf<string, NotFound, Error>>(new NotFound());
        }

        public UniTask<OneOf<Success, Error>> WriteAsync(string relativePath, string content, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Files[relativePath] = content;
            WrittenPaths.Add(relativePath);
            return UniTask.FromResult<OneOf<Success, Error>>(new Success());
        }

        public OneOf<Success, Error> Delete(string relativePath)
        {
            Files.Remove(relativePath);
            return new Success();
        }
    }
}
