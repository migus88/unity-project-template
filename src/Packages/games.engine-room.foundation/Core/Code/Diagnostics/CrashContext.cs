using System;
using System.IO;
using System.Threading;
using Core.Results;
using Core.Storage;
using OneOf;

namespace Core.Diagnostics
{
    public sealed class CrashContext : ICrashContext
    {
        private const byte NewLine = (byte)'\n';

        public string? SessionFilePath => Volatile.Read(ref _sessionFilePath);

        public string? SessionFileName
        {
            get
            {
                var path = SessionFilePath;
                return path == null ? null : Path.GetFileName(path);
            }
        }

        public string? LogFilePath { get; }

        private string? _sessionFilePath;

        private readonly string _storageRoot;

        public CrashContext(string storageRoot, string? logFilePath)
        {
            if (!Path.IsPathRooted(storageRoot))
            {
                throw new ArgumentException($"Storage root must be an absolute path: '{storageRoot}'.", nameof(storageRoot));
            }

            _storageRoot = storageRoot;
            LogFilePath = string.IsNullOrEmpty(logFilePath) ? null : logFilePath;
        }

        public void SetSessionFile(string storagePath)
        {
            Volatile.Write(ref _sessionFilePath, StoragePaths.Combine(_storageRoot, storagePath));
        }

        public void ClearSessionFile()
        {
            Volatile.Write(ref _sessionFilePath, null);
        }

        public OneOf<byte[], NotFound, Error> ReadLogTail(int maxBytes)
        {
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "The tail size must be positive.");
            }

            if (LogFilePath == null)
            {
                return new NotFound();
            }

            try
            {
                using var stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var start = Math.Max(0, stream.Length - maxBytes);
                stream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[stream.Length - start];
                var read = ReadFully(stream, buffer);
                return start == 0 ? Slice(buffer, 0, read) : DropFirstLine(buffer, read);
            }
            catch (FileNotFoundException)
            {
                return new NotFound();
            }
            catch (DirectoryNotFoundException)
            {
                return new NotFound();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new Error($"Failed to read the log '{LogFilePath}': {exception.Message}");
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        private static byte[] DropFirstLine(byte[] buffer, int length)
        {
            var newLine = Array.IndexOf(buffer, NewLine, 0, length);
            return newLine < 0 ? Slice(buffer, 0, length) : Slice(buffer, newLine + 1, length - newLine - 1);
        }

        private static byte[] Slice(byte[] buffer, int offset, int length)
        {
            if (offset == 0 && length == buffer.Length)
            {
                return buffer;
            }

            var slice = new byte[length];
            Buffer.BlockCopy(buffer, offset, slice, 0, length);
            return slice;
        }
    }
}
