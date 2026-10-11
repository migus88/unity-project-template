using System;
using System.IO;

namespace Core.Storage
{
    internal static class StoragePaths
    {
        public static string Combine(string rootDirectory, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException($"Path must be a non-empty relative path: '{relativePath}'.", nameof(relativePath));
            }

            return Path.Combine(rootDirectory, relativePath);
        }
    }
}
