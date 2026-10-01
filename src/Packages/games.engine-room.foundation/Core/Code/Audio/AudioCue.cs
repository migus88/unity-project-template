using UnityEngine;
using UnityEngine.Audio;

namespace Core.Audio
{
    [CreateAssetMenu(menuName = "Core/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [field: SerializeField] public AudioClip[] Clips { get; private set; } = [];
        [field: SerializeField] public AudioMixerGroup Group { get; private set; } = null!;
        [field: SerializeField, Range(0f, 1f)] public float Volume { get; private set; } = 1f;
        [field: SerializeField] public Vector2 PitchRange { get; private set; } = Vector2.one;
        [field: SerializeField] public bool Loop { get; private set; }
    }
}
