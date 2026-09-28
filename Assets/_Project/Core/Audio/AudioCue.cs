using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Core.Audio
{
    [CreateAssetMenu(menuName = "Core/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [field: SerializeField, Required] public AudioClip[] Clips { get; private set; } = [];
        [field: SerializeField, Required] public AudioMixerGroup Group { get; private set; } = null!;
        [field: SerializeField, Range(0f, 1f)] public float Volume { get; private set; } = 1f;
        [field: SerializeField, MinMaxSlider(0.1f, 3f, true)] public Vector2 PitchRange { get; private set; } = Vector2.one;
        [field: SerializeField] public bool Loop { get; private set; }
    }
}
