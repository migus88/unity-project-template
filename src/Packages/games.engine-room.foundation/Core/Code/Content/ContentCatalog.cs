using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Content
{
    public sealed class ContentCatalog : ScriptableObject
    {
        [SerializeField] private ContentCatalogEntry[] _entries = Array.Empty<ContentCatalogEntry>();

        public IReadOnlyList<ContentCatalogEntry> Entries => _entries;

        public static ContentCatalog Create(IReadOnlyList<ContentCatalogEntry> entries)
        {
            var catalog = CreateInstance<ContentCatalog>();
            var copy = new ContentCatalogEntry[entries.Count];

            for (var i = 0; i < entries.Count; i++)
            {
                copy[i] = entries[i];
            }

            catalog._entries = copy;
            return catalog;
        }
    }
}
