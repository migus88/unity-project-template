using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Cheats;
using Core.Input;
using Cysharp.Threading.Tasks;
using Migs.MLock.Interfaces;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Cheats
{
    public sealed class CheatConsolePresenterTests
    {
        private const string Pending = "pending";

        private CheatConsoleViewFixture _fixture = null!;
        private CheatRegistry _registry = null!;
        private IInputService _input = null!;
        private ILockService<InputLockTag> _locks = null!;
        private IDisposable _mapsHandle = null!;
        private ILock<InputLockTag> _lock = null!;
        private CheatConsolePresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _fixture = new CheatConsoleViewFixture();
            _registry = new CheatRegistry();
            _input = Substitute.For<IInputService>();
            _locks = Substitute.For<ILockService<InputLockTag>>();
            _mapsHandle = Substitute.For<IDisposable>();
            _lock = Substitute.For<ILock<InputLockTag>>();
            _input.Push(Arg.Any<InputMaps>()).Returns(_mapsHandle);
            _locks.LockAll().Returns(_lock);
            _presenter = new CheatConsolePresenter(_fixture.View, _registry, new CheatConsoleModel(_registry), _input, _locks);
            _presenter.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _fixture.Dispose();
        }

        [Test]
        public void Start_Authored_HidesTheConsoleAndAddsClear()
        {
            // Assert
            _fixture.View.IsShown.Should().BeFalse();
            _registry.Find(ClearConsoleCheat.CheatName).Should().NotBeNull();
        }

        [Test]
        public void Toggle_Closed_OpensWithoutGameMapsAndLocksEveryTag()
        {
            // Act
            _presenter.Toggle();

            // Assert
            _fixture.View.IsShown.Should().BeTrue();
            _input.Received(1).Push(InputMaps.None);
            _locks.Received(1).LockAll();
        }

        [Test]
        public void Toggle_Open_ClosesAndReleasesMapsAndLock()
        {
            // Arrange
            _presenter.Toggle();

            // Act
            _presenter.Toggle();

            // Assert
            _fixture.View.IsShown.Should().BeFalse();
            _mapsHandle.Received(1).Dispose();
            _lock.Received(1).Dispose();
        }

        [Test]
        public void Close_Closed_DoesNothing()
        {
            // Act
            _presenter.Close();

            // Assert
            _mapsHandle.DidNotReceive().Dispose();
            _lock.DidNotReceive().Dispose();
        }

        [Test]
        public void Dispose_Open_ReleasesMapsAndLock()
        {
            // Arrange
            _presenter.Toggle();

            // Act
            _presenter.Dispose();

            // Assert
            _mapsHandle.Received(1).Dispose();
            _lock.Received(1).Dispose();
        }

        [Test]
        public async Task Submit_ValidLine_SetsLastReplyAndAppendsTheOutput()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold", "Added 5 gold.", CheatParameter.Int("amount")));

            // Act
            await SubmitAsync("gold 5");

            // Assert
            _fixture.View.LastReply.Should().Be("Added 5 gold.");
            _fixture.Output.text.Should().Be("<noparse>> gold 5</noparse>\n<noparse>Added 5 gold.</noparse>");
        }

        [Test]
        public async Task Submit_ConsoleClosed_StillRunsTheCheat()
        {
            // Arrange
            var cheat = new RecordingCheat("heal");
            _registry.Add(cheat);

            // Act
            await SubmitAsync("heal");

            // Assert
            cheat.Calls.Should().Be(1);
            _fixture.View.IsShown.Should().BeFalse();
        }

        [Test]
        public async Task Submit_ThrowingCheat_RepliesWithTheErrorAndKeepsWorking()
        {
            // Arrange
            _registry.Add(new ThrowingCheat());
            _registry.Add(new RecordingCheat("heal", "Healed."));
            LogAssert.Expect(LogType.Exception, new Regex("kaboom"));

            // Act
            await SubmitAsync("boom");
            var errorReply = _fixture.View.LastReply;
            await SubmitAsync("heal");

            // Assert
            errorReply.Should().Be("Error: kaboom");
            _fixture.View.LastReply.Should().Be("Healed.");
        }

        [Test]
        public async Task Submit_Clear_EmptiesTheOutput()
        {
            // Arrange
            _registry.Add(new RecordingCheat("heal", "Healed."));
            await SubmitAsync("heal");

            // Act
            await SubmitAsync("clear");

            // Assert
            _fixture.Output.text.Should().BeEmpty();
        }

        [Test]
        public void LineChanged_CommandPrefix_ShowsSuggestions()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("god"));
            _registry.Add(new RecordingCheat("goto"));
            _presenter.Toggle();

            // Act
            _fixture.Input.text = "go";

            // Assert
            _fixture.Suggestions.activeSelf.Should().BeTrue();
            _fixture.Labels[0].text.Should().Be("god");
            _fixture.Labels[1].text.Should().Be("gold");
            _fixture.More.text.Should().Be("+1 more");
        }

        [Test]
        public void CompleteLine_Open_CompletesTheLine()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _presenter.Toggle();
            _fixture.Input.text = "gol";

            // Act
            _presenter.CompleteLine();

            // Assert
            _fixture.View.Line.Should().Be("gold ");
        }

        [Test]
        public void CompleteLine_Cycling_HighlightsTheSelectedSuggestion()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("god"));
            _presenter.Toggle();
            _fixture.Input.text = "go";

            // Act
            _presenter.CompleteLine();
            _presenter.CompleteLine();

            // Assert
            _fixture.View.Line.Should().Be("gold");
            _fixture.Highlights[0].enabled.Should().BeFalse();
            _fixture.Highlights[1].enabled.Should().BeTrue();
        }

        [Test]
        public async Task MoveUp_NoList_RecallsHistory()
        {
            // Arrange
            _registry.Add(new RecordingCheat("heal", "Healed."));
            _presenter.Toggle();
            await SubmitAsync("heal");

            // Act
            _presenter.MoveUp();

            // Assert
            _fixture.View.Line.Should().Be("heal");
        }

        [Test]
        public async Task MoveUp_AfterRecall_KeepsRecallingHistory()
        {
            // Arrange
            _registry.Add(new RecordingCheat("heal", "Healed."));
            _registry.Add(new RecordingCheat("kill", "Killed."));
            _presenter.Toggle();
            await SubmitAsync("heal");
            await SubmitAsync("kill");
            _presenter.MoveUp();

            // Act
            _presenter.MoveUp();

            // Assert
            _fixture.View.Line.Should().Be("heal");
            _fixture.Suggestions.activeSelf.Should().BeFalse();
        }

        [Test]
        public void MoveDown_ListOpen_HighlightsFirst()
        {
            // Arrange
            OpenWithGoCheats();

            // Act
            _presenter.MoveDown();

            // Assert
            _fixture.Highlights[0].enabled.Should().BeTrue();
            _fixture.Highlights[1].enabled.Should().BeFalse();
            _fixture.View.Line.Should().Be("go");
        }

        [Test]
        public void MoveUp_ListOpen_HighlightsLast()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("god"));
            _presenter.Toggle();
            _fixture.Input.text = "go";

            // Act
            _presenter.MoveUp();

            // Assert
            _fixture.Highlights[0].enabled.Should().BeFalse();
            _fixture.Highlights[1].enabled.Should().BeTrue();
        }

        [Test]
        public void MoveDown_LastSelected_WrapsToFirst()
        {
            // Arrange
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("god"));
            _presenter.Toggle();
            _fixture.Input.text = "go";
            _presenter.MoveUp();

            // Act
            _presenter.MoveDown();

            // Assert
            _fixture.Highlights[0].enabled.Should().BeTrue();
            _fixture.Highlights[1].enabled.Should().BeFalse();
        }

        [Test]
        public void MoveDown_PastVisibleRows_ScrollsTheWindow()
        {
            // Arrange
            OpenWithGoCheats();

            // Act
            _presenter.MoveDown();
            _presenter.MoveDown();
            _presenter.MoveDown();

            // Assert
            _fixture.Labels[0].text.Should().Be("gold");
            _fixture.Labels[1].text.Should().Be("goto");
            _fixture.Highlights[0].enabled.Should().BeFalse();
            _fixture.Highlights[1].enabled.Should().BeTrue();
            _fixture.More.text.Should().Be("+1 more");
        }

        [Test]
        public void CompleteLine_ArrowSelected_AcceptsSelection()
        {
            // Arrange
            OpenWithGoCheats();
            _presenter.MoveDown();
            _presenter.MoveDown();

            // Act
            _presenter.CompleteLine();

            // Assert
            _fixture.View.Line.Should().Be("gold ");
            _fixture.Suggestions.activeSelf.Should().BeFalse();
        }

        [Test]
        public void CompleteLine_NoSelection_KeepsCompletion()
        {
            // Arrange
            OpenWithGoCheats();

            // Act
            _presenter.CompleteLine();

            // Assert
            _fixture.View.Line.Should().Be("god");
            _fixture.Highlights[0].enabled.Should().BeTrue();
        }

        [Test]
        public void Enter_ArrowSelected_AcceptsWithoutRunning()
        {
            // Arrange
            var gold = new RecordingCheat("gold", CheatParameter.Int("amount"));
            _registry.Add(gold);
            _registry.Add(new RecordingCheat("god"));
            _presenter.Toggle();
            _fixture.Input.text = "go";
            _presenter.MoveUp();

            // Act
            _fixture.Input.onSubmit.Invoke("go");

            // Assert
            gold.Calls.Should().Be(0);
            _fixture.View.Line.Should().Be("gold ");
            _fixture.View.IsShown.Should().BeTrue();
        }

        [Test]
        public async Task Enter_NoSelection_RunsTheLine()
        {
            // Arrange
            var heal = new RecordingCheat("heal", "Healed.");
            _registry.Add(heal);
            _presenter.Toggle();
            _fixture.View.LastReply = Pending;

            // Act
            _fixture.Input.onSubmit.Invoke("heal");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UniTask.WaitUntil(() => _fixture.View.LastReply != Pending, cancellationToken: timeout.Token);

            // Assert
            heal.Calls.Should().Be(1);
            _fixture.View.LastReply.Should().Be("Healed.");
        }

        [Test]
        public void Cancel_ListOpen_HidesListAndKeepsConsoleOpen()
        {
            // Arrange
            OpenWithGoCheats();
            _presenter.MoveDown();

            // Act
            _presenter.Cancel();

            // Assert
            _fixture.Suggestions.activeSelf.Should().BeFalse();
            _fixture.View.IsShown.Should().BeTrue();
            _fixture.View.Line.Should().Be("go");
            _mapsHandle.DidNotReceive().Dispose();
        }

        [Test]
        public void Cancel_NoList_ClosesConsole()
        {
            // Arrange
            _presenter.Toggle();

            // Act
            _presenter.Cancel();

            // Assert
            _fixture.View.IsShown.Should().BeFalse();
            _mapsHandle.Received(1).Dispose();
        }

        [Test]
        public void LineChanged_AfterDismiss_ShowsListAgain()
        {
            // Arrange
            OpenWithGoCheats();
            _presenter.Cancel();

            // Act
            _fixture.Input.text = "gol";

            // Assert
            _fixture.Suggestions.activeSelf.Should().BeTrue();
            _fixture.Labels[0].text.Should().Be("gold");
        }

        private void OpenWithGoCheats()
        {
            _registry.Add(new RecordingCheat("gold"));
            _registry.Add(new RecordingCheat("god"));
            _registry.Add(new RecordingCheat("goto"));
            _presenter.Toggle();
            _fixture.Input.text = "go";
        }

        private async Task SubmitAsync(string line)
        {
            _fixture.View.LastReply = Pending;
            _fixture.View.Submit(line);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UniTask.WaitUntil(() => _fixture.View.LastReply != Pending, cancellationToken: timeout.Token);
        }
    }
}
