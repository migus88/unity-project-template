using Core.Logging;
using UnityEngine;

namespace Core.Domains
{
    public abstract class DomainDescriptor : ScriptableObject
    {
        [field: SerializeField] public string ContentDirectoryName { get; private set; } = null!;
        [field: SerializeField] public LogTag LogTag { get; private set; }

#if UNITY_EDITOR
        [field: SerializeField] public DomainContent EditorContent { get; private set; } = null!;
#endif
    }
}
