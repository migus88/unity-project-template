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
            if (_handles.TryGetValue(descriptor.ContentDirectoryName, out var handle))
            {
                return GetRootContent(descriptor.ContentDirectoryName, handle);
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
        }

        private OneOf<DomainContent, NotFound> GetRootContent(string name, ContentDirectoryHandle handle)
        {
            var rootAssets = _loadManager.GetRootAssets<DomainContent>(handle);

            if (rootAssets.Length == 0)
            {
                return new NotFound();
            }

            if (rootAssets.Length > 1)
            {
                throw new InvalidOperationException($"Content directory '{name}' has {rootAssets.Length} {nameof(DomainContent)} root assets. Expected exactly one.");
            }

            return rootAssets[0];
        }
    }
}
