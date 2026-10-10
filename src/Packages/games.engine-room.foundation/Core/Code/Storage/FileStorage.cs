using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core.Results;
using Cysharp.Threading.Tasks;
using OneOf;
using Success = OneOf.Types.Success;

namespace Core.Storage
{
    public sealed class FileStorage : IFileStorage
    {
        private const string TempFileSuffix = ".tmp";

        private readonly string _rootDirectory;

        public FileStorage(string rootDirectory)
        {
            if (!Path.IsPathRooted(rootDirectory))
            {
                throw new ArgumentException($"Root directory must be an absolute path: '{rootDirectory}'.", nameof(rootDirectory));
            }

            _rootDirectory = rootDirectory;
        }

        public bool Exists(string relativePath)
        {
            return File.Exists(ToFullPath(relativePath));
        }

        public UniTask<OneOf<string, NotFound, Error>> ReadAsync(string relativePath, CancellationToken ct)
        {
            return ReadFromDiskAsync(relativePath, (path, token) => File.ReadAllTextAsync(path, token), ct);
        }

        public UniTask<OneOf<Success, Error>> WriteAsync(string relativePath, string content, CancellationToken ct)
        {
            return WriteReplacingAsync(relativePath, (path, token) => WriteTextToDiskAsync(path, content, token), ct);
        }

        public UniTask<OneOf<byte[], NotFound, Error>> ReadBytesAsync(string relativePath, CancellationToken ct)
        {
            return ReadFromDiskAsync(relativePath, (path, token) => File.ReadAllBytesAsync(path, token), ct);
        }

        public UniTask<OneOf<Success, Error>> WriteBytesAsync(string relativePath, byte[] content, CancellationToken ct)
        {
            return WriteReplacingAsync(relativePath, (path, token) => WriteBytesToDiskAsync(path, content, token), ct);
        }

        public OneOf<Success, Error> Delete(string relativePath)
        {
            var fullPath = ToFullPath(relativePath);

            try
            {
                File.Delete(fullPath);
                return new Success();
            }
            catch (DirectoryNotFoundException)
            {
                return new Success();
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
                return new Error($"Failed to delete '{fullPath}': {exception.Message}");
            }
        }

        private async UniTask<OneOf<T, NotFound, Error>> ReadFromDiskAsync<T>(string relativePath, Func<string, CancellationToken, Task<T>> read, CancellationToken ct)
        {
            var fullPath = ToFullPath(relativePath);

            try
            {
                return await read(fullPath, ct);
            }
            catch (FileNotFoundException)
            {
                return new NotFound();
            }
            catch (DirectoryNotFoundException)
            {
                return new NotFound();
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
                return new Error($"Failed to read '{fullPath}': {exception.Message}");
            }
        }

        private async UniTask<OneOf<Success, Error>> WriteReplacingAsync(string relativePath, Func<string, CancellationToken, UniTask> writeTemp, CancellationToken ct)
        {
            var fullPath = ToFullPath(relativePath);
            var tempPath = fullPath + TempFileSuffix;
            var isReplaced = false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await writeTemp(tempPath, ct);
                ReplaceWith(tempPath, fullPath);
                isReplaced = true;
                return new Success();
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
                return new Error($"Failed to write '{fullPath}': {exception.Message}");
            }
            finally
            {
                if (!isReplaced)
                {
                    DeleteTempFile(tempPath);
                }
            }
        }

        private static async UniTask WriteTextToDiskAsync(string path, string content, CancellationToken ct)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content.AsMemory(), ct);
            await writer.FlushAsync();
            stream.Flush(true);
        }

        private static async UniTask WriteBytesToDiskAsync(string path, byte[] content, CancellationToken ct)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(content, 0, content.Length, ct);
            stream.Flush(true);
        }

        private static void ReplaceWith(string tempPath, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, null);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }

        private static void DeleteTempFile(string tempPath)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
            }
        }

        private static bool IsFileSystemFailure(Exception exception)
        {
            return exception is IOException or UnauthorizedAccessException;
        }

        private string ToFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException($"Path must be a non-empty relative path: '{relativePath}'.", nameof(relativePath));
            }

            return Path.Combine(_rootDirectory, relativePath);
        }
    }
}
