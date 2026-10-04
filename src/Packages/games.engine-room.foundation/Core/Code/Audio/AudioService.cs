using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;
using VContainer.Unity;

namespace Core.Audio
{
    public sealed class AudioService : IAudioService, ILateTickable, IDisposable
    {
        private int _musicVersion;
        private int _nextLoopId;
        private bool _isDisposed;
        private AudioCue? _currentMusicCue;

        private readonly AudioMixer _mixer;
        private readonly AudioCuePicker _picker;
        private readonly Stack<AudioSource> _freeSources = new();
        private readonly List<ActiveSource> _activeSources = new();
        private readonly MusicSlot _musicA;
        private readonly MusicSlot _musicB;

        public AudioService(AudioMixer mixer, IReadOnlyList<AudioSource> sfxSources, AudioSource musicSourceA, AudioSource musicSourceB)
            : this(mixer, sfxSources, musicSourceA, musicSourceB, new AudioCuePicker(new System.Random()))
        {
        }

        internal AudioService(AudioMixer mixer, IReadOnlyList<AudioSource> sfxSources, AudioSource musicSourceA, AudioSource musicSourceB, AudioCuePicker picker)
        {
            if (sfxSources.Count == 0)
            {
                throw new ArgumentException("At least one SFX audio source must be authored.", nameof(sfxSources));
            }

            _mixer = mixer;
            _picker = picker;

            for (var i = sfxSources.Count - 1; i >= 0; i--)
            {
                _freeSources.Push(sfxSources[i]);
            }

            _musicA = new MusicSlot(musicSourceA);
            _musicB = new MusicSlot(musicSourceB);
        }

        public void Play(AudioCue cue)
        {
            PlaySfx(cue, null, null);
        }

        public void PlayAt(AudioCue cue, Vector3 position)
        {
            PlaySfx(cue, position, null);
        }

        public void PlayAttached(AudioCue cue, Transform target)
        {
            PlaySfx(cue, target.position, target);
        }

        public AudioLoop PlayLoop(AudioCue cue)
        {
            if (!cue.Loop)
            {
                throw new ArgumentException($"Audio cue '{cue.name}' does not loop and cannot be played as a loop.", nameof(cue));
            }

            var source = StartSource(cue, true, null);
            var id = ++_nextLoopId;
            _activeSources.Add(new ActiveSource(source, null, id));
            return new AudioLoop(this, id);
        }

        public UniTask PlayMusicAsync(AudioCue cue, float crossfadeSeconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (cue == _currentMusicCue)
            {
                return WaitForMusicAsync(_musicVersion, ct);
            }

            var clip = _picker.PickClip(cue);
            var incoming = _musicA.Volume <= _musicB.Volume ? _musicA : _musicB;
            var outgoing = incoming == _musicA ? _musicB : _musicA;

            incoming.Start(clip, cue, _picker.PickPitch(cue));
            incoming.FadeTo(cue.Volume, crossfadeSeconds);
            outgoing.FadeTo(0f, crossfadeSeconds);
            _currentMusicCue = cue;
            _musicVersion++;

            return WaitForMusicAsync(_musicVersion, ct);
        }

        public UniTask StopMusicAsync(float fadeSeconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            _musicA.FadeTo(0f, fadeSeconds);
            _musicB.FadeTo(0f, fadeSeconds);
            _currentMusicCue = null;
            _musicVersion++;

            return WaitForMusicAsync(_musicVersion, ct);
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            var parameterName = AudioVolume.GetParameterName(channel);

            if (!_mixer.SetFloat(parameterName, AudioVolume.ToDecibels(volume)))
            {
                throw new InvalidOperationException($"Audio mixer '{_mixer.name}' has no exposed parameter '{parameterName}'.");
            }
        }

        public void LateTick()
        {
            Advance(UnityEngine.Time.unscaledDeltaTime);
        }

        internal void Advance(float deltaSeconds)
        {
            for (var i = _activeSources.Count - 1; i >= 0; i--)
            {
                var active = _activeSources[i];

                if (!active.IsLoop && !active.Source.isPlaying)
                {
                    active.Source.clip = null;
                    _freeSources.Push(active.Source);
                    _activeSources.RemoveAt(i);
                    continue;
                }

                if (active.Target != null)
                {
                    active.Source.transform.position = active.Target.position;
                }
            }

            _musicA.Advance(deltaSeconds);
            _musicB.Advance(deltaSeconds);
        }

        internal void StopLoop(int id)
        {
            if (_isDisposed || id == 0)
            {
                return;
            }

            var index = FindLoopIndex(id);

            if (index < 0)
            {
                return;
            }

            var source = _activeSources[index].Source;
            _activeSources.RemoveAt(index);
            source.Stop();
            source.clip = null;
            _freeSources.Push(source);
        }

        internal bool IsLoopPlaying(int id)
        {
            return id != 0 && FindLoopIndex(id) >= 0;
        }

        private void PlaySfx(AudioCue cue, Vector3? position, Transform? target)
        {
            if (cue.Loop)
            {
                throw new ArgumentException($"Audio cue '{cue.name}' loops and can only be played as music or through PlayLoop.", nameof(cue));
            }

            var source = StartSource(cue, false, position);
            _activeSources.Add(new ActiveSource(source, target, 0));
        }

        private AudioSource StartSource(AudioCue cue, bool isLooping, Vector3? position)
        {
            var clip = _picker.PickClip(cue);
            var source = TakeSfxSource();

            source.clip = clip;
            source.outputAudioMixerGroup = cue.Group;
            source.volume = cue.Volume;
            source.pitch = _picker.PickPitch(cue);
            source.loop = isLooping;
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.transform.localPosition = Vector3.zero;

            if (position.HasValue)
            {
                source.transform.position = position.Value;
            }

            source.Play();
            return source;
        }

        private int FindLoopIndex(int id)
        {
            for (var i = 0; i < _activeSources.Count; i++)
            {
                if (_activeSources[i].LoopId == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private async UniTask WaitForMusicAsync(int version, CancellationToken ct)
        {
            await UniTask.WaitUntil(() => _isDisposed || version != _musicVersion || (_musicA.IsSettled && _musicB.IsSettled), PlayerLoopTiming.Update, ct);
        }

        private AudioSource TakeSfxSource()
        {
            if (_freeSources.Count > 0)
            {
                return _freeSources.Pop();
            }

            for (var i = 0; i < _activeSources.Count; i++)
            {
                var active = _activeSources[i];

                if (!active.IsLoop && !active.Source.isPlaying)
                {
                    _activeSources.RemoveAt(i);
                    return active.Source;
                }
            }

            var stolenIndex = FindOldestOneShotIndex();
            var stolen = _activeSources[stolenIndex];
            _activeSources.RemoveAt(stolenIndex);
            stolen.Source.Stop();
            return stolen.Source;
        }

        private int FindOldestOneShotIndex()
        {
            for (var i = 0; i < _activeSources.Count; i++)
            {
                if (!_activeSources[i].IsLoop)
                {
                    return i;
                }
            }

            return 0;
        }

        public void Dispose()
        {
            _isDisposed = true;

            foreach (var active in _activeSources)
            {
                active.Source.Stop();
            }

            _activeSources.Clear();
            _musicA.Stop();
            _musicB.Stop();
        }

        private readonly record struct ActiveSource(AudioSource Source, Transform? Target, int LoopId)
        {
            public bool IsLoop => LoopId != 0;
        }

        private sealed class MusicSlot
        {
            public float Volume { get; private set; }
            public bool IsSettled => Volume == _targetVolume && (_targetVolume > 0f || _source.clip == null);

            private float _targetVolume;
            private float _volumePerSecond;

            private readonly AudioSource _source;

            public MusicSlot(AudioSource source)
            {
                _source = source;
            }

            public void Start(AudioClip clip, AudioCue cue, float pitch)
            {
                _source.Stop();
                _source.clip = clip;
                _source.outputAudioMixerGroup = cue.Group;
                _source.pitch = pitch;
                _source.loop = cue.Loop;
                _source.spatialBlend = 0f;
                SetVolume(0f);
                _source.Play();
            }

            public void FadeTo(float targetVolume, float seconds)
            {
                _targetVolume = targetVolume;
                _volumePerSecond = seconds > 0f ? Mathf.Abs(targetVolume - Volume) / seconds : float.PositiveInfinity;

                if (seconds <= 0f)
                {
                    Advance(0f);
                }
            }

            public void Advance(float deltaSeconds)
            {
                if (Volume != _targetVolume)
                {
                    var step = float.IsPositiveInfinity(_volumePerSecond) ? float.PositiveInfinity : _volumePerSecond * deltaSeconds;
                    SetVolume(Mathf.MoveTowards(Volume, _targetVolume, step));
                }

                if (_targetVolume == 0f && Volume == 0f && _source.clip != null)
                {
                    Stop();
                }
            }

            public void Stop()
            {
                _source.Stop();
                _source.clip = null;
                _targetVolume = 0f;
                SetVolume(0f);
            }

            private void SetVolume(float volume)
            {
                Volume = volume;
                _source.volume = volume;
            }
        }
    }
}
