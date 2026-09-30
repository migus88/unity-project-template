using System.Collections.Generic;
using UnityEngine;

namespace Core.Audio
{
    public sealed class AudioSourceSet : MonoBehaviour
    {
        public IReadOnlyList<AudioSource> SfxSources => _sfxSources;
        public AudioSource MusicSourceA => _musicSourceA;
        public AudioSource MusicSourceB => _musicSourceB;

        [SerializeField] private AudioSource[] _sfxSources = [];
        [SerializeField] private AudioSource _musicSourceA = null!;
        [SerializeField] private AudioSource _musicSourceB = null!;
    }
}
