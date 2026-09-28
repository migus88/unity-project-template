using Core.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Core.Tests.Audio
{
    internal static class TestAudioCues
    {
        public const string MixerPath = "Assets/_Project/Core/Audio/GameAudioMixer.mixer";

        public static AudioMixer LoadMixer()
        {
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        public static AudioClip CreateClip(string name)
        {
            return AudioClip.Create(name, 44100, 1, 44100, false);
        }

        public static AudioCue Create(AudioClip?[] clips, AudioMixerGroup? group, float volume = 1f, Vector2? pitchRange = null, bool isLooping = false)
        {
            var cue = ScriptableObject.CreateInstance<AudioCue>();
            var serializedCue = new SerializedObject(cue);
            var clipsProperty = serializedCue.FindProperty("<Clips>k__BackingField");
            clipsProperty.arraySize = clips.Length;

            for (var i = 0; i < clips.Length; i++)
            {
                clipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            serializedCue.FindProperty("<Group>k__BackingField").objectReferenceValue = group;
            serializedCue.FindProperty("<Volume>k__BackingField").floatValue = volume;
            serializedCue.FindProperty("<PitchRange>k__BackingField").vector2Value = pitchRange ?? Vector2.one;
            serializedCue.FindProperty("<Loop>k__BackingField").boolValue = isLooping;
            serializedCue.ApplyModifiedPropertiesWithoutUndo();
            return cue;
        }
    }
}
