using System;
using System.Collections.Generic;
using System.IO;
using Core.Domains;
using Core.Logging;
using Core.Results;
using OneOf;
using Unity.Loading;

namespace Core.Content
{
    public sealed class ContentDirectoryRegistry : IContentDirectoryRegistry
    {
        public const string RootFolderName = "Content";

        private readonly IContentLoadManager _loadManager;
        private readonly Dictionary<string, ContentDirectoryHandle> _handles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CatalogedContent> _contents = new(StringComparer.Ordinal);

        public ContentDirectoryRegistry(IContentLoadManager loadManager)
        {
            _loadManager = loadManager;
        }

        public void RegisterAll(string rootPath)
        {
            if (!Path.IsPathRooted(rootPath))
            {
                throw new ArgumentException($"Content root must be an absolute path: '{rootPath}'.", nameof(rootPath));
            }

            if (!Directory.Exists(rootPath))
            {
                Log.Warn(LogTags.Content, $"No content directories found at '{rootPath}'. Build them with Build/Content Directories.");
                return;
            }

            var directoryPaths = Directory.GetDirectories(rootPath);
            Array.Sort(directoryPaths, StringComparer.Ordinal);

            foreach (var directoryPath in directoryPaths)
            {
                Register(Path.GetFileName(directoryPath), directoryPath);
            }
        }

        public OneOf<ContentDirectoryHandle, NotFound> Get(string name)
        {
            if (_handles.TryGetValue(name, out var handle))
            {
                return handle;
            }

            return new NotFound();
        }

        public OneOf<DomainContent, NotFound> GetContent(DomainDescriptor descriptor)
        {
            if (_contents.TryGetValue(descriptor.ContentDirectoryName, out var cataloged))
            {
                return cataloged.Content;
            }

#if UNITY_EDITOR
            if (descriptor.EditorContent != null)
            {
                return descriptor.EditorContent;
            }
#endif

            return new NotFound();
        }

        private void Register(string name, string path)
        {
            if (_handles.ContainsKey(name))
            {
                throw new InvalidOperationException($"Content directory '{name}' is already registered.");
            }

            var handle = _loadManager.RegisterContentDirectory(path);

            if (!handle.IsValid)
            {
                Log.Error(LogTags.Content, $"Failed to register content directory '{name}' at '{path}'.");
                return;
            }

            _handles.Add(name, handle);
            ReadCatalog(name, handle);
        }

        private void ReadCatalog(string directoryName, ContentDirectoryHandle handle)
        {
            var catalogs = _loadManager.GetRootAssets<ContentCatalog>(handle);

            if (catalogs.Length != 1)
            {
                Log.Error(LogTags.Content, $"Content directory '{directoryName}' has {catalogs.Length} content catalogs, expected exactly one. Rebuild with Build/Content Directories.");
                return;
            }

            foreach (var entry in catalogs[0].Entries)
            {
                if (_contents.TryGetValue(entry.Name, out var other))
                {
                    throw new InvalidOperationException($"Content '{entry.Name}' is in content directories '{other.DirectoryName}' and '{directoryName}'. Rebuild with Build/Content Directories.");
                }

                _contents.Add(entry.Name, new CatalogedContent(directoryName, entry.Content));
            }
        }

        private readonly record struct CatalogedContent(string DirectoryName, DomainContent Content);
    }
}
