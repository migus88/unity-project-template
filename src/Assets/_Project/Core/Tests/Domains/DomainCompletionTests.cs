using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Domains;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Core.Tests.Domains
{
    public sealed class DomainCompletionTests
    {
        [Test]
        public void IsCompleted_NotCompleted_ReturnsFalse()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();

            // Act
            var isCompleted = completion.IsCompleted;

            // Assert
            isCompleted.Should().BeFalse();
        }

        [Test]
        public async Task Complete_FirstCall_CompletesTaskWithResult()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            var expected = new TestDomainResult(7);

            // Act
            completion.Complete(expected);

            // Assert
            completion.IsCompleted.Should().BeTrue();
            var result = await completion.Task;
            result.Should().BeSameAs(expected);
        }

        [Test]
        public async Task Task_AwaitedBeforeComplete_ResumesWithResult()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            var pending = completion.Task.AsTask();

            // Act
            completion.Complete(new TestDomainResult(3));

            // Assert
            var result = await pending;
            result.Value.Should().Be(3);
        }

        [Test]
        public async Task Complete_SecondCall_ThrowsAndKeepsFirstResult()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            completion.Complete(new TestDomainResult(1));

            // Act
            Action act = () => completion.Complete(new TestDomainResult(2));

            // Assert
            act.Should().Throw<InvalidOperationException>();
            var result = await completion.Task;
            result.Value.Should().Be(1);
        }

        [Test]
        public async Task Fail_NotCompleted_FaultsTaskWithException()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            var exception = new InvalidOperationException("The domain failed.");

            // Act
            ((IDomainCompletion)completion).Fail(exception);

            // Assert
            completion.IsCompleted.Should().BeTrue();
            Func<Task> awaitTask = () => completion.Task.AsTask();
            var thrown = await awaitTask.Should().ThrowAsync<InvalidOperationException>();
            thrown.Which.Should().BeSameAs(exception);
        }

        [Test]
        public async Task Fail_AfterComplete_KeepsResult()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            completion.Complete(new TestDomainResult(5));

            // Act
            ((IDomainCompletion)completion).Fail(new InvalidOperationException("The domain failed."));

            // Assert
            var result = await completion.Task;
            result.Value.Should().Be(5);
        }

        [Test]
        public async Task Complete_AfterFail_ThrowsAndKeepsFailure()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();
            ((IDomainCompletion)completion).Fail(new ArgumentException("The domain failed."));

            // Act
            Action act = () => completion.Complete(new TestDomainResult(1));

            // Assert
            act.Should().Throw<InvalidOperationException>();
            Func<Task> awaitTask = () => completion.Task.AsTask();
            await awaitTask.Should().ThrowAsync<ArgumentException>();
        }

        [Test]
        public void Complete_NullResult_Throws()
        {
            // Arrange
            var completion = new DomainCompletion<TestDomainResult>();

            // Act
            Action act = () => completion.Complete(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
            completion.IsCompleted.Should().BeFalse();
        }
    }
}
