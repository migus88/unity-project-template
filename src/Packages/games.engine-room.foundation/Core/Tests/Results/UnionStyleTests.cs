using System;
using AwesomeAssertions;
using Core.Results;
using NUnit.Framework;
using OneOf;
using TestUtils;

namespace Core.Tests.Results
{
    public sealed class UnionStyleTests
    {
        [Test]
        public void BeCase_MatchingCase_ReturnsCaseValue()
        {
            // Arrange
            SampleResult result = new SampleResult.Done(7);

            // Act
            var done = result.Should().BeCase<SampleResult.Done>().Which;

            // Assert
            done.Count.Should().Be(7);
        }

        [Test]
        public void BeCase_OtherCase_Fails()
        {
            // Arrange
            OneOf<string, NotFound, Error> result = new Error("disk full");

            // Act
            Action act = () => result.Should().BeCase<NotFound>();

            // Assert
            act.Should().Throw<AssertionException>().WithMessage("*NotFound*Error*");
        }

        [Test]
        public void NotBeCase_SameCase_Fails()
        {
            // Arrange
            OneOf<string, NotFound> result = new NotFound();

            // Act
            Action act = () => result.Should().NotBeCase<NotFound>();

            // Assert
            act.Should().Throw<AssertionException>();
        }

        [Test]
        public void NotBeCase_OtherCase_Passes()
        {
            // Arrange
            OneOf<string, NotFound> result = "value";

            // Act
            Action act = () => result.Should().NotBeCase<NotFound>();

            // Assert
            act.Should().NotThrow();
        }
    }

    [GenerateOneOf]
    public sealed partial class SampleResult : OneOfBase<SampleResult.Done, NotFound, SampleResult.Cancelled>
    {
        public readonly record struct Done(int Count);

        public readonly record struct Cancelled;
    }
}
