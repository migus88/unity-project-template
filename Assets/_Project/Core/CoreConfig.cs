using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Core
{
    [CreateAssetMenu(menuName = "Core/Core Config")]
    public sealed class CoreConfig : ScriptableObject
    {
        [field: SerializeField, Required] public AudioMixer AudioMixer { get; private set; } = null!;
        [field: SerializeField, MinValue(0)] public int AudioSourcePoolSize { get; private set; } = 16;
    }
}
