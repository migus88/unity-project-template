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
        public void GeneratedUnion_FromNestedCase_MatchesThatCase()
        {
            // Arrange
            SampleResult result = new SampleResult.Done(3);

            // Act
            var description = result.Match(
                done => $"done {done.Count}",
                notFound => "not found",
                cancelled => "cancelled");

            // Assert
            description.Should().Be("done 3");
        }

        [Test]
        public void GeneratedUnion_FromSharedCase_MatchesThatCase()
        {
            // Arrange
            SampleResult result = new NotFound();

            // Act
            var isNotFound = result.Match(
                done => false,
                notFound => true,
                cancelled => false);

            // Assert
            isNotFound.Should().BeTrue();
        }

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
            act.Should().Throw<Exception>().WithMessage("*NotFound*Error*");
        }

        [Test]
        public void NotBeCase_SameCase_Fails()
        {
            // Arrange
            OneOf<string, NotFound> result = new NotFound();

            // Act
            Action act = () => result.Should().NotBeCase<NotFound>();

            // Assert
            act.Should().Throw<Exception>();
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

        [Test]
        public void TryPickT0_OtherCase_ReturnsRemainder()
        {
            // Arrange
            OneOf<string, NotFound, Error> result = new Error("broken");

            // Act
            var isString = result.TryPickT0(out _, out var remainder);

            // Assert
            isString.Should().BeFalse();
            remainder.Should().BeCase<Error>().Which.Message.Should().Be("broken");
        }
    }

    [GenerateOneOf]
    public sealed partial class SampleResult : OneOfBase<SampleResult.Done, NotFound, SampleResult.Cancelled>
    {
        public readonly record struct Done(int Count);

        public readonly record struct Cancelled;
    }
}
