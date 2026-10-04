using System;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Core.Tests.Audio
{
    public sealed class AudioCuePickerTests
    {
        private AudioClip _first = null!;
        private AudioClip _second = null!;
        private AudioClip _third = null!;
        private AudioMixerGroup _group = null!;
        private AudioCue? _cue;

        [SetUp]
        public void SetUp()
        {
            _first = TestAudioCues.CreateClip("First");
            _second = TestAudioCues.CreateClip("Second");
            _third = TestAudioCues.CreateClip("Third");
            _group = TestAudioCues.LoadMixer().FindMatchingGroups("Sfx")[0];
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            Object.DestroyImmediate(_third);

            if (_cue != null)
            {
                Object.DestroyImmediate(_cue);
            }
        }

        [TestCase(0, "First")]
        [TestCase(2, "Third")]
        public void PickClip_SeveralClips_ReturnsClipAtRandomIndex(int index, string expected)
        {
            // Arrange
            _cue = TestAudioCues.Create([_first, _second, _third], _group);
            var picker = new AudioCuePicker(new FixedRandom(index, 0d));

            // Act
            var clip = picker.PickClip(_cue);

            // Assert
            clip.name.Should().Be(expected);
        }

        [Test]
        public void PickClip_SeveralClips_AsksRandomForIndexBelowClipCount()
        {
            // Arrange
            _cue = TestAudioCues.Create([_first, _second, _third], _group);
            var random = new FixedRandom(0, 0d);
            var picker = new AudioCuePicker(random);

            // Act
            picker.PickClip(_cue);

            // Assert
            random.LastMaxValue.Should().Be(3);
        }

        [Test]
        public void PickClip_NoClips_Throws()
        {
            // Arrange
            _cue = TestAudioCues.Create([], _group);
            var picker = new AudioCuePicker(new FixedRandom(0, 0d));

            // Act
            Action act = () => picker.PickClip(_cue);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void PickClip_EmptyClipSlot_Throws()
        {
            // Arrange
            _cue = TestAudioCues.Create([_first, null], _group);
            var picker = new AudioCuePicker(new FixedRandom(0, 0d));

            // Act
            Action act = () => picker.PickClip(_cue);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void PickClip_NoGroup_Throws()
        {
            // Arrange
            _cue = TestAudioCues.Create([_first], null);
            var picker = new AudioCuePicker(new FixedRandom(0, 0d));

            // Act
            Action act = () => picker.PickClip(_cue);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [TestCase(0d, 0.8f)]
        [TestCase(1d, 1.2f)]
        public void PickPitch_Range_InterpolatesBetweenMinAndMax(double sample, float expected)
        {
            // Arrange
            _cue = TestAudioCues.Create([_first], _group, pitchRange: new Vector2(0.8f, 1.2f));
            var picker = new AudioCuePicker(new FixedRandom(0, sample));

            // Act
            var pitch = picker.PickPitch(_cue);

            // Assert
            pitch.Should().BeApproximately(expected, 0.0001f);
        }

        private sealed class FixedRandom : System.Random
        {
            public int LastMaxValue { get; private set; }

            private readonly int _index;
            private readonly double _sample;

            public FixedRandom(int index, double sample)
            {
                _index = index;
                _sample = sample;
            }

            public override int Next(int maxValue)
            {
                LastMaxValue = maxValue;
                return _index;
            }

            public override double NextDouble()
            {
                return _sample;
            }
        }
    }
}
