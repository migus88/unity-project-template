using System;
using System.IO;
using System.Threading;
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

        public async UniTask<OneOf<string, NotFound, Error>> ReadAsync(string relativePath, CancellationToken ct)
        {
            var fullPath = ToFullPath(relativePath);

            try
            {
                return await File.ReadAllTextAsync(fullPath, ct);
            }
            catch (FileNotFoundException)
            {
                return new NotFound();
            }
            catch (DirectoryNotFoundException)
            {
                return new NotFound();
            }
            catch (IOException exception)
            {
                return new Error($"Failed to read '{fullPath}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new Error($"Failed to read '{fullPath}': {exception.Message}");
            }
        }

        public async UniTask<OneOf<Success, Error>> WriteAsync(string relativePath, string content, CancellationToken ct)
        {
            var fullPath = ToFullPath(relativePath);
            var tempPath = fullPath + TempFileSuffix;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(tempPath, content, ct);

                if (File.Exists(fullPath))
                {
                    File.Replace(tempPath, fullPath, null);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }

                return new Success();
            }
            catch (IOException exception)
            {
                return new Error($"Failed to write '{fullPath}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new Error($"Failed to write '{fullPath}': {exception.Message}");
            }
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
            catch (IOException exception)
            {
                return new Error($"Failed to delete '{fullPath}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new Error($"Failed to delete '{fullPath}': {exception.Message}");
            }
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
