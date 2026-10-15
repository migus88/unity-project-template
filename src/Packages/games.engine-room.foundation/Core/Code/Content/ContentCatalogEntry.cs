using System;
using Core.Domains;
using UnityEngine;

namespace Core.Content
{
    [Serializable]
    public struct ContentCatalogEntry
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public DomainContent Content { get; private set; }

        public ContentCatalogEntry(string name, DomainContent content)
        {
            Name = name;
            Content = content;
        }
    }
}
