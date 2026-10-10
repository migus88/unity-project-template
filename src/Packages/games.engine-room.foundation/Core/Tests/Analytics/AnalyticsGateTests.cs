using AwesomeAssertions;
using Core.Analytics;
using NSubstitute;
using NUnit.Framework;

namespace Core.Tests.Analytics
{
    public sealed class AnalyticsGateTests
    {
        [Test]
        public void Create_Enabled_ForwardsEventToBackend()
        {
            // Arrange
            var backend = Substitute.For<IAnalyticsBackend>();
            var analytics = AnalyticsGate.Create(true, backend);

            // Act
            analytics.Track(new ProbeEvent(3));

            // Assert
            backend.Received(1).Send(new ProbeEvent(3));
        }

        [Test]
        public void Create_Disabled_NeverReachesBackend()
        {
            // Arrange
            var backend = Substitute.For<IAnalyticsBackend>();
            var analytics = AnalyticsGate.Create(false, backend);

            // Act
            analytics.Track(new ProbeEvent(3));

            // Assert
            backend.DidNotReceiveWithAnyArgs().Send(default(ProbeEvent));
            analytics.Should().BeOfType<NullAnalytics>();
        }

        private readonly record struct ProbeEvent(int Value) : IAnalyticsEvent
        {
            public string Name => "probe";

            public void Write(IAnalyticsWriter writer)
            {
                writer.Add("value", Value);
            }
        }
    }
}
