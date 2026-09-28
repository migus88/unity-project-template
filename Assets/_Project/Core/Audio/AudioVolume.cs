using System;
using UnityEngine;

namespace Core.Audio
{
    public static class AudioVolume
    {
        public const float MinDecibels = -80f;

        private const float MinLinear = 0.0001f;

        public static float ToDecibels(float volume)
        {
            var linear = Mathf.Clamp01(volume);
            return linear <= MinLinear ? MinDecibels : 20f * Mathf.Log10(linear);
        }

        public static string GetParameterName(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Master => "MasterVolume",
                AudioChannel.Music => "MusicVolume",
                AudioChannel.Sfx => "SfxVolume",
                AudioChannel.Ui => "UiVolume",
                _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Audio channel has no mixer parameter."),
            };
        }
    }
}
