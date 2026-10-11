using UnityEngine;

namespace Core.Environment
{
    [CreateAssetMenu(menuName = "Core/Active Build Environment")]
    public sealed class ActiveBuildEnvironment : ScriptableObject
    {
        [field: SerializeField] public BuildEnvironment? Environment { get; private set; }
    }
}
