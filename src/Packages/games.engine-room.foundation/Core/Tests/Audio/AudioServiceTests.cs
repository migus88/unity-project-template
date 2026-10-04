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
        private AudioSource[] _sfxSources = null!;
        private AudioSource _musicA = null!;
        private AudioSource _musicB = null!;
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
            _sfxSources = [CreateSource("Sfx 1"), CreateSource("Sfx 2")];
            _musicA = CreateSource("Music A");
            _musicB = CreateSource("Music B");
            _service = new AudioService(mixer, _sfxSources, _musicA, _musicB, new AudioCuePicker(new System.Random(1)));
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
        public void Constructor_NoSfxSources_Throws()
        {
            // Act
            Action act = () => new AudioService(TestAudioCues.LoadMixer(), Array.Empty<AudioSource>(), _musicA, _musicB);

            // Assert
            act.Should().Throw<ArgumentException>();
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
        public void Play_FreeSources_UsesDifferentAuthoredSources()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));

            // Act
            _service.Play(first);
            _service.Play(second);

            // Assert
            FindSfxSourceWithClip(_clip).Should().NotBeSameAs(FindSfxSourceWithClip(_otherClip));
        }

        [Test]
        public void Play_AllSourcesBusy_StealsOldestStartedSourceWithoutCreatingOne()
        {
            // Arrange
            var thirdClip = Track(TestAudioCues.CreateClip("ThirdClip"));
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var third = Track(TestAudioCues.Create([thirdClip], _sfxGroup));
            _service.Play(first);
            var oldest = FindSfxSourceWithClip(_clip);
            _service.Play(second);

            // Act
            _service.Play(third);

            // Assert
            oldest.clip.Should().Be(thirdClip);
            FindSfxSourceWithClip(_otherClip).Should().NotBeSameAs(oldest);
            _root.GetComponentsInChildren<AudioSource>().Should().HaveCount(4);
        }

        [Test]
        public void Play_NoFreeSourceButOneFinished_ReusesFinishedSourceAndKeepsOldestPlaying()
        {
            // Arrange
            var thirdClip = Track(TestAudioCues.CreateClip("ThirdClip"));
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var third = Track(TestAudioCues.Create([thirdClip], _sfxGroup));
            _service.Play(first);
            var oldest = FindSfxSourceWithClip(_clip);
            _service.Play(second);
            var finished = FindSfxSourceWithClip(_otherClip);
            finished.Stop();

            // Act
            _service.Play(third);

            // Assert
            finished.clip.Should().Be(thirdClip);
            oldest.clip.Should().Be(_clip);
            oldest.isPlaying.Should().BeTrue();
        }

        [Test]
        public void Play_AllSourcesBusyTwice_StealsInStartOrder()
        {
            // Arrange
            var thirdClip = Track(TestAudioCues.CreateClip("ThirdClip"));
            var fourthClip = Track(TestAudioCues.CreateClip("FourthClip"));
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var third = Track(TestAudioCues.Create([thirdClip], _sfxGroup));
            var fourth = Track(TestAudioCues.Create([fourthClip], _sfxGroup));
            _service.Play(first);
            var firstSource = FindSfxSourceWithClip(_clip);
            _service.Play(second);
            var secondSource = FindSfxSourceWithClip(_otherClip);

            // Act
            _service.Play(third);
            _service.Play(fourth);

            // Assert
            firstSource.clip.Should().Be(thirdClip);
            secondSource.clip.Should().Be(fourthClip);
        }

        [Test]
        public void Advance_SfxFinished_ReusesSourceForNextPlay()
        {
            // Arrange
            var first = Track(TestAudioCues.Create([_clip], _sfxGroup));
            var second = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            _service.Play(first);
            var finished = FindSfxSourceWithClip(_clip);
            finished.Stop();

            // Act
            _service.Advance(0f);
            _service.Play(second);

            // Assert
            FindSfxSourceWithClip(_otherClip).Should().BeSameAs(finished);
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
        public void PlayLoop_LoopingCue_PlaysLooping2DSourceWithCueSettings()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, volume: 0.5f, isLooping: true));

            // Act
            var loop = _service.PlayLoop(cue);

            // Assert
            var source = FindSfxSourceWithClip(_clip);
            source.loop.Should().BeTrue();
            source.outputAudioMixerGroup.Should().Be(_sfxGroup);
            source.volume.Should().Be(0.5f);
            source.spatialBlend.Should().Be(0f);
            loop.IsPlaying.Should().BeTrue();
        }

        [Test]
        public void PlayLoop_NonLoopingCue_Throws()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup));

            // Act
            Action act = () => _service.PlayLoop(cue);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Stop_PlayingLoop_StopsSourceAndFreesIt()
        {
            // Arrange
            var loopCue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var oneShot = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var loop = _service.PlayLoop(loopCue);
            var loopSource = FindSfxSourceWithClip(_clip);

            // Act
            loop.Stop();
            _service.Play(oneShot);

            // Assert
            loop.IsPlaying.Should().BeFalse();
            FindSfxSourceWithClip(_otherClip).Should().BeSameAs(loopSource);
        }

        [Test]
        public void Stop_Twice_DoesNotStopNewerSoundOnSameSource()
        {
            // Arrange
            var loopCue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var oneShot = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var loop = _service.PlayLoop(loopCue);
            loop.Stop();
            _service.Play(oneShot);
            var oneShotSource = FindSfxSourceWithClip(_otherClip);

            // Act
            loop.Stop();

            // Assert
            oneShotSource.isPlaying.Should().BeTrue();
            oneShotSource.clip.Should().Be(_otherClip);
        }

        [Test]
        public void Dispose_Handle_StopsLoop()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var loop = _service.PlayLoop(cue);
            var source = FindSfxSourceWithClip(_clip);

            // Act
            loop.Dispose();

            // Assert
            loop.IsPlaying.Should().BeFalse();
            source.isPlaying.Should().BeFalse();
        }

        [Test]
        public void Stop_DefaultHandle_DoesNothing()
        {
            // Arrange
            var loop = default(AudioLoop);

            // Act
            Action act = () => loop.Stop();

            // Assert
            act.Should().NotThrow();
            loop.IsPlaying.Should().BeFalse();
        }

        [Test]
        public void Dispose_Service_StopsPlayingLoop()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var loop = _service.PlayLoop(cue);
            var source = FindSfxSourceWithClip(_clip);

            // Act
            _service.Dispose();

            // Assert
            source.isPlaying.Should().BeFalse();
            loop.IsPlaying.Should().BeFalse();
        }

        [Test]
        public void Stop_AfterServiceDispose_DoesNothing()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var loop = _service.PlayLoop(cue);
            _service.Dispose();

            // Act
            Action act = () => loop.Stop();

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void Advance_LoopNotPlaying_KeepsLoopActive()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var loop = _service.PlayLoop(cue);
            var source = FindSfxSourceWithClip(_clip);
            source.Pause();

            // Act
            _service.Advance(1f);

            // Assert
            loop.IsPlaying.Should().BeTrue();
            source.clip.Should().Be(_clip);
        }

        [Test]
        public void Play_AllSourcesBusyWithLoopAndOneShot_StealsOneShot()
        {
            // Arrange
            var thirdClip = Track(TestAudioCues.CreateClip("ThirdClip"));
            var loopCue = Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true));
            var oneShot = Track(TestAudioCues.Create([_otherClip], _sfxGroup));
            var third = Track(TestAudioCues.Create([thirdClip], _sfxGroup));
            var loop = _service.PlayLoop(loopCue);
            var loopSource = FindSfxSourceWithClip(_clip);
            _service.Play(oneShot);
            var oneShotSource = FindSfxSourceWithClip(_otherClip);

            // Act
            _service.Play(third);

            // Assert
            oneShotSource.clip.Should().Be(thirdClip);
            loopSource.clip.Should().Be(_clip);
            loop.IsPlaying.Should().BeTrue();
        }

        [Test]
        public void Play_AllSourcesBusyWithLoops_StealsOldestLoopAndSilencesItsHandle()
        {
            // Arrange
            var thirdClip = Track(TestAudioCues.CreateClip("ThirdClip"));
            var firstLoop = _service.PlayLoop(Track(TestAudioCues.Create([_clip], _sfxGroup, isLooping: true)));
            var firstSource = FindSfxSourceWithClip(_clip);
            var secondLoop = _service.PlayLoop(Track(TestAudioCues.Create([_otherClip], _sfxGroup, isLooping: true)));

            // Act
            _service.Play(Track(TestAudioCues.Create([thirdClip], _sfxGroup)));
            firstLoop.Stop();

            // Assert
            firstLoop.IsPlaying.Should().BeFalse();
            firstSource.clip.Should().Be(thirdClip);
            firstSource.loop.Should().BeFalse();
            firstSource.isPlaying.Should().BeTrue();
            secondLoop.IsPlaying.Should().BeTrue();
        }

        [Test]
        public void PlayMusicAsync_NoCrossfade_StartsMusicAtCueVolume()
        {
            // Arrange
            var cue = Track(TestAudioCues.Create([_clip], _musicGroup, volume: 0.8f, isLooping: true));

            // Act
            _service.PlayMusicAsync(cue, 0f, CancellationToken.None);

            // Assert
            var music = _musicA;
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
            _musicA.volume.Should().BeApproximately(0.5f, 0.0001f);
            _musicB.clip.Should().Be(_otherClip);
            _musicB.volume.Should().BeApproximately(0.5f, 0.0001f);
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
            _musicA.clip.Should().BeNull();
            _musicA.volume.Should().Be(0f);
            _musicB.volume.Should().Be(1f);
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
            _musicA.volume.Should().Be(1f);
            _musicB.clip.Should().BeNull();
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
            _musicA.clip.Should().BeNull();
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
            _musicA.volume.Should().BeApproximately(0.5f, 0.0001f);
            _musicA.clip.Should().Be(_clip);
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
            _musicA.clip.Should().BeNull();
            _musicA.volume.Should().Be(0f);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        private AudioSource CreateSource(string name)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(_root.transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private AudioSource FindSfxSourceWithClip(AudioClip clip)
        {
            foreach (var source in _sfxSources)
            {
                if (source.clip == clip)
                {
                    return source;
                }
            }

            throw new AssertionException($"No SFX source plays {clip.name}.");
        }
    }
}
