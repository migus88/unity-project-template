using System;
using Core.Audio;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Core.Tests.Audio
{
    public sealed class NullUiInteractionSoundsTests
    {
        [Test]
        public void Play_EveryInteraction_DoesNothingAndLogsNothing()
        {
            // Arrange
            var sounds = new NullUiInteractionSounds();

            // Act
            foreach (UiInteraction interaction in Enum.GetValues(typeof(UiInteraction)))
            {
                sounds.Play(interaction);
            }

            // Assert
            LogAssert.NoUnexpectedReceived();
        }
    }
}
