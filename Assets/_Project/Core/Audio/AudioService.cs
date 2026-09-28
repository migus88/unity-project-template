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
        private int _sourceCount;
        private int _musicVersion;
        private bool _isDisposed;
        private AudioCue? _currentMusicCue;

        private readonly AudioMixer _mixer;
        private readonly Transform _root;
        private readonly AudioCuePicker _picker;
        private readonly Stack<AudioSource> _freeSources = new();
        private readonly List<ActiveSource> _activeSources = new();
        private readonly MusicSlot _musicA;
        private readonly MusicSlot _musicB;

        public AudioService(AudioMixer mixer, Transform root, int poolSize)
            : this(mixer, root, poolSize, new AudioCuePicker(new System.Random()))
        {
        }

        internal AudioService(AudioMixer mixer, Transform root, int poolSize, AudioCuePicker picker)
        {
            _mixer = mixer;
            _root = root;
            _picker = picker;

            for (var i = 0; i < poolSize; i++)
            {
                _freeSources.Push(CreateSfxSource());
            }

            _musicA = new MusicSlot(CreateSource("Music A"));
            _musicB = new MusicSlot(CreateSource("Music B"));
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

                if (!active.Source.isPlaying)
                {
                    active.Source.clip = null;
                    _freeSources.Push(active.Source);
                    _activeSources[i] = _activeSources[^1];
                    _activeSources.RemoveAt(_activeSources.Count - 1);
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

        private void PlaySfx(AudioCue cue, Vector3? position, Transform? target)
        {
            if (cue.Loop)
            {
                throw new ArgumentException($"Audio cue '{cue.name}' loops and can only be played as music.", nameof(cue));
            }

            var clip = _picker.PickClip(cue);
            var source = _freeSources.Count > 0 ? _freeSources.Pop() : CreateSfxSource();

            source.clip = clip;
            source.outputAudioMixerGroup = cue.Group;
            source.volume = cue.Volume;
            source.pitch = _picker.PickPitch(cue);
            source.loop = false;
            source.spatialBlend = position.HasValue ? 1f : 0f;
            source.transform.position = position ?? _root.position;
            source.Play();

            _activeSources.Add(new ActiveSource(source, target));
        }

        private async UniTask WaitForMusicAsync(int version, CancellationToken ct)
        {
            await UniTask.WaitUntil(() => _isDisposed || version != _musicVersion || (_musicA.IsSettled && _musicB.IsSettled), PlayerLoopTiming.Update, ct);
        }

        private AudioSource CreateSfxSource()
        {
            _sourceCount++;
            return CreateSource($"Sfx Source {_sourceCount}");
        }

        private AudioSource CreateSource(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(_root, false);
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
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

        private readonly struct ActiveSource
        {
            public AudioSource Source { get; }
            public Transform? Target { get; }

            public ActiveSource(AudioSource source, Transform? target)
            {
                Source = source;
                Target = target;
            }
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
