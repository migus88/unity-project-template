using Core.Logging;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Domains
{
    public abstract class DomainDescriptor : ScriptableObject
    {
        [field: SerializeField, Required] public string ContentDirectoryName { get; private set; } = null!;
        [field: SerializeField] public LogTag LogTag { get; private set; }

#if UNITY_EDITOR
        [field: SerializeField, Required] public DomainContent EditorContent { get; private set; } = null!;
#endif
    }
}
