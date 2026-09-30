using System;
using AwesomeAssertions;
using Core.Audio;
using NUnit.Framework;

namespace Core.Tests.Audio
{
    public sealed class AudioVolumeTests
    {
        [TestCase(1f, 0f)]
        [TestCase(0.1f, -20f)]
        [TestCase(0.01f, -40f)]
        public void ToDecibels_LinearVolume_ReturnsDecibels(float volume, float expected)
        {
            // Act
            var decibels = AudioVolume.ToDecibels(volume);

            // Assert
            decibels.Should().BeApproximately(expected, 0.001f);
        }

        [Test]
        public void ToDecibels_Half_ReturnsAboutMinusSix()
        {
            // Act
            var decibels = AudioVolume.ToDecibels(0.5f);

            // Assert
            decibels.Should().BeApproximately(-6.0206f, 0.001f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(0.00001f)]
        public void ToDecibels_SilentOrBelow_ReturnsMinDecibels(float volume)
        {
            // Act
            var decibels = AudioVolume.ToDecibels(volume);

            // Assert
            decibels.Should().Be(AudioVolume.MinDecibels);
        }

        [Test]
        public void ToDecibels_AboveOne_ClampsToZero()
        {
            // Act
            var decibels = AudioVolume.ToDecibels(2f);

            // Assert
            decibels.Should().Be(0f);
        }

        [TestCase(AudioChannel.Master, "MasterVolume")]
        [TestCase(AudioChannel.Music, "MusicVolume")]
        [TestCase(AudioChannel.Sfx, "SfxVolume")]
        [TestCase(AudioChannel.Ui, "UiVolume")]
        public void GetParameterName_Channel_ReturnsExposedParameterName(AudioChannel channel, string expected)
        {
            // Act
            var parameterName = AudioVolume.GetParameterName(channel);

            // Assert
            parameterName.Should().Be(expected);
        }

        [Test]
        public void GetParameterName_None_Throws()
        {
            // Act
            Action act = () => AudioVolume.GetParameterName(AudioChannel.None);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
