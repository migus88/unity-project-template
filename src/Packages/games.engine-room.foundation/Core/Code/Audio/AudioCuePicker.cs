using System;
using UnityEngine;

namespace Core.Audio
{
    internal sealed class AudioCuePicker
    {
        private readonly System.Random _random;

        public AudioCuePicker(System.Random random)
        {
            _random = random;
        }

        public AudioClip PickClip(AudioCue cue)
        {
            Validate(cue);
            return cue.Clips[_random.Next(cue.Clips.Length)];
        }

        public float PickPitch(AudioCue cue)
        {
            return Mathf.Lerp(cue.PitchRange.x, cue.PitchRange.y, (float)_random.NextDouble());
        }

        private static void Validate(AudioCue cue)
        {
            if (cue.Clips.Length == 0)
            {
                throw new ArgumentException($"Audio cue '{cue.name}' has no clips.", nameof(cue));
            }

            foreach (var clip in cue.Clips)
            {
                if (clip == null)
                {
                    throw new ArgumentException($"Audio cue '{cue.name}' has an empty clip slot.", nameof(cue));
                }
            }

            if (cue.Group == null)
            {
                throw new ArgumentException($"Audio cue '{cue.name}' has no mixer group.", nameof(cue));
            }
        }
    }
}
