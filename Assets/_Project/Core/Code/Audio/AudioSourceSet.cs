using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Audio
{
    public sealed class AudioSourceSet : MonoBehaviour
    {
        [SerializeField, Required] private AudioSource[] _sfxSources = [];
        [SerializeField, Required] private AudioSource _musicSourceA = null!;
        [SerializeField, Required] private AudioSource _musicSourceB = null!;

        public IReadOnlyList<AudioSource> SfxSources => _sfxSources;
        public AudioSource MusicSourceA => _musicSourceA;
        public AudioSource MusicSourceB => _musicSourceB;
    }
}
