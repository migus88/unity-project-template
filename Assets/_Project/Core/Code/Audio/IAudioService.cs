using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Audio
{
    public interface IAudioService
    {
        void Play(AudioCue cue);
        void PlayAt(AudioCue cue, Vector3 position);
        void PlayAttached(AudioCue cue, Transform target);
        UniTask PlayMusicAsync(AudioCue cue, float crossfadeSeconds, CancellationToken ct);
        UniTask StopMusicAsync(float fadeSeconds, CancellationToken ct);
        void SetVolume(AudioChannel channel, float volume);
    }
}
