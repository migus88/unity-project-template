using System;
using System.Collections.Generic;
using System.Threading;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Core.Tests.Audio
{
    public sealed class AudioServiceTests
    {
        private AudioMixerGroup _sfxGroup = null!;
        private AudioMixerGroup _musicGroup = null!;
        private AudioClip _clip = null!;
        private AudioClip _otherClip = null!;
        private GameObject _root = null!;
        private AudioService _service = null!;

        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            var mixer = TestAudioCues.LoadMixer();
            _sfxGroup = mixer.FindMatchingGroups("Sfx")[0];
            _musicGroup = mixer.FindMatchingGroups("Music")[0];
            _clip = TestAudioCues.CreateClip("Clip");
            _otherClip = TestAudioCues.CreateClip("OtherClip");
            _root = new GameObject("AudioRoot");
            _service = new AudioService(mixer, _root.transform, 1, new AudioCuePicker(new System.Random(1)));
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_clip);
            Object.DestroyImmediate(_otherClip);

            foreach (var createdObject in _createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Constructor_PoolSize_CreatesSfxAndMusicSourcesUnderRoot()
        {
            // Act
            var sources = _root.GetComponentsInChildren<AudioSource>();

            // Assert
            sources.Should().HaveCount(3);
            sources.Should().OnlyContain(source => !source.playOnAwake);
            _root.transform.Find("Music A").Should().NotBeNull();
            _root.transform.Find("Music B").Should().NotBeNull();
        }

        [Test]
        public void Play_Cue_Plays2DSourceWithCueSettings()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, volume: 0.5f, pitchRange: new Vector2(1.5f, 1.5f)));

            // Act
            _service.Play(cue);

            // Assert
            var source = FindSfxSourceWithClip(_clip);
            source.outputAudioMixerGroup.Should().Be(_sfxGroup);
            source.volume.Should().Be(0.5f);
            source.pitch.Should().Be(1.5f);
            source.loop.Should().BeFalse();
            source.spatialBlend.Should().Be(0f);
        }

        [Test]
        public void PlayAt_Position_Plays3DSourceAtPosition()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var position = new Vector3(1f, 2f, 3f);

            // Act
            _service.PlayAt(cue, position);

            // Assert
            var source = FindSfxSourceWithClip(_clip);
            source.spatialBlend.Should().Be(1f);
            source.transform.position.Should().Be(position);
        }

        [Test]
        public void PlayAttached_Target_Plays3DSourceAtTargetPosition()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var target = Track(new GameObject("Target"));
            target.transform.position = new Vector3(4f, 5f, 6f);

            // Act
            _service.PlayAttached(cue, target.transform);

            // Assert
            var source = FindSfxSourceWithClip(_clip);
            source.spatialBlend.Should().Be(1f);
            source.transform.position.Should().Be(target.transform.position);
        }

        [Test]
        public void Play_PoolExhausted_CreatesAnotherSource()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));

            // Act
            _service.Play(first);
            _service.Play(second);

            // Assert
            _root.GetComponentsInChildren<AudioSource>().Should().HaveCount(4);
            FindSfxSourceWithClip(_clip).Should().NotBeSameAs(FindSfxSourceWithClip(_otherClip));
        }

        [Test]
        public void Advance_SfxFinished_ReusesSourceForNextPlay()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            _service.Play(first);
            FindSfxSourceWithClip(_clip).Stop();

            // Act
            _service.Advance(0f);
            _service.Play(second);

            // Assert
            _root.GetComponentsInChildren<AudioSource>().Should().HaveCount(3);
            FindSfxSourceWithClip(_otherClip).Should().NotBeNull();
        }

        [Test]
        public void Play_LoopingCue_Throws()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));

            // Act
            Action act = () => _service.Play(cue);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void PlayMusicAsync_NoCrossfade_StartsMusicAtCueVolume()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, volume: 0.8f, isLooping: true));

            // Act
            _service.PlayMusicAsync(cue, 0f, CancellationToken.None);

            // Assert
            var music = FindMusicSource("Music A");
            music.clip.Should().Be(_clip);
            music.outputAudioMixerGroup.Should().Be(_musicGroup);
            music.loop.Should().BeTrue();
            music.volume.Should().Be(0.8f);
        }

        [Test]
        public void PlayMusicAsync_HalfwayThroughCrossfade_BothTracksAtHalfVolume()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            var second = Track(TestAudioCues.Create([_otherClip], _musicGroup, isLooping: true));
            _service.PlayMusicAsync(first, 0f, CancellationToken.None);

            // Act
            _service.PlayMusicAsync(second, 2f, CancellationToken.None);
            _service.Advance(1f);

            // Assert
            FindMusicSource("Music A").volume.Should().BeApproximately(0.5f, 0.0001f);
            FindMusicSource("Music B").clip.Should().Be(_otherClip);
            FindMusicSource("Music B").volume.Should().BeApproximately(0.5f, 0.0001f);
        }

        [Test]
        public void PlayMusicAsync_CrossfadeFinished_StopsPreviousTrack()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            var second = Track(TestAudioCues.Create([_otherClip], _musicGroup, isLooping: true));
            _service.PlayMusicAsync(first, 0f, CancellationToken.None);

            // Act
            _service.PlayMusicAsync(second, 2f, CancellationToken.None);
            _service.Advance(2f);

            // Assert
            FindMusicSource("Music A").clip.Should().BeNull();
            FindMusicSource("Music A").volume.Should().Be(0f);
            FindMusicSource("Music B").volume.Should().Be(1f);
        }

        [Test]
        public void PlayMusicAsync_SameCueAgain_KeepsCurrentTrack()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            _service.PlayMusicAsync(cue, 0f, CancellationToken.None);

            // Act
            _service.PlayMusicAsync(cue, 2f, CancellationToken.None);

            // Assert
            FindMusicSource("Music A").volume.Should().Be(1f);
            FindMusicSource("Music B").clip.Should().BeNull();
        }

        [Test]
        public void PlayMusicAsync_CancelledToken_Throws()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Action act = () => _service.PlayMusicAsync(cue, 0f, cts.Token);

            // Assert
            act.Should().Throw<OperationCanceledException>();
            FindMusicSource("Music A").clip.Should().BeNull();
        }

        [Test]
        public void StopMusicAsync_HalfwayThroughFade_HalvesVolume()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            _service.PlayMusicAsync(cue, 0f, CancellationToken.None);

            // Act
            _service.StopMusicAsync(2f, CancellationToken.None);
            _service.Advance(1f);

            // Assert
            FindMusicSource("Music A").volume.Should().BeApproximately(0.5f, 0.0001f);
            FindMusicSource("Music A").clip.Should().Be(_clip);
        }

        [Test]
        public void StopMusicAsync_NoFade_StopsImmediately()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, isLooping: true));
            _service.PlayMusicAsync(cue, 0f, CancellationToken.None);

            // Act
            _service.StopMusicAsync(0f, CancellationToken.None);

            // Assert
            FindMusicSource("Music A").clip.Should().BeNull();
            FindMusicSource("Music A").volume.Should().Be(0f);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        private AudioSource FindSfxSourceWithClip(AudioClip clip)
        {
            foreach (var source in _root.GetComponentsInChildren<AudioSource>())
            {
                if (source.clip == clip && source.name.StartsWith("Sfx Source", StringComparison.Ordinal))
                {
                    return source;
                }
            }

            throw new AssertionException($"No SFX source plays {clip.name}.");
        }

        private AudioSource FindMusicSource(string name)
        {
            return _root.transform.Find(name).GetComponent<AudioSource>();
        }
    }
}
