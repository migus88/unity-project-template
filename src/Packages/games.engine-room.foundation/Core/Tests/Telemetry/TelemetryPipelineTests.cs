using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Analytics;
using Core.Environment;
using Core.Http;
using Core.Privacy;
using Core.RemoteConfig;
using Core.Storage;
using Core.Telemetry;
using Core.Tests.Privacy;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.Telemetry
{
    public sealed class TelemetryPipelineTests
    {
        private static readonly DateTime Start = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        private static readonly TestTelemetryEvent Clicked = new("button_clicked");

        private InMemoryFileStorage _storage = null!;
        private JsonSerializer _serializer = null!;
        private TelemetryQueueStore _store = null!;
        private FakeHttpService _http = null!;
        private FakeClock _clock = null!;
        private ConsentService _consent = null!;
        private IRemoteConfigService _remoteConfig = null!;
        private IApplicationService _application = null!;
        private AnalyticsFlushSignal _flushSignal = null!;
        private Func<CancellationToken, UniTask>? _quitHandler;
        private TelemetryPipeline _pipeline = null!;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemoryFileStorage();
            _serializer = new JsonSerializer();
            _store = new TelemetryQueueStore(_storage, _serializer);
            _http = new FakeHttpService();
            _clock = new FakeClock(Start);
            _consent = new ConsentService(new FakeSettingsService());
            _remoteConfig = Substitute.For<IRemoteConfigService>();
            _remoteConfig.Get(CoreFlags.TelemetryEnabled).Returns(true);
            _application = Substitute.For<IApplicationService>();
            _application.AddQuitHandler(Arg.Do<Func<CancellationToken, UniTask>>(handler => _quitHandler = handler)).Returns(Substitute.For<IDisposable>());
            _flushSignal = new AnalyticsFlushSignal();
            _quitHandler = null;
            _pipeline = CreatePipeline(TelemetryTestData.Options);
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline.Dispose();
            _consent.Dispose();
        }

        [Test]
        public void Send_ConsentUnknown_DropsEventAndWritesNothing()
        {
            // Arrange
            _http.Respond(_ => TelemetryTestData.Status(202));

            // Act
            _pipeline.Send(Clicked);
            TickAfter(TimeSpan.FromSeconds(30));

            // Assert
            _http.Requests.Should().BeEmpty();
            _storage.Files.Should().BeEmpty();
        }

        [Test]
        public async Task Send_EnvironmentNotAllowed_DropsEventAndWritesNothing()
        {
            // Arrange
            _pipeline.Dispose();
            _pipeline = CreatePipeline(TelemetryOptions.None);
            _http.Respond(_ => TelemetryTestData.Status(202));
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            _pipeline.Send(Clicked);
            TickAfter(TimeSpan.FromSeconds(30));

            // Assert
            _http.Requests.Should().BeEmpty();
            _storage.Files.Should().BeEmpty();
        }

        [Test]
        public async Task Send_ReportingNotAllowed_WritesAndSendsNothing()
        {
            // Arrange
            _pipeline.Dispose();
            _pipeline = CreatePipeline(TelemetryTestData.Options, new ReportingRule(true, Substitute.For<IReportingOverride>()));
            _http.Respond(_ => TelemetryTestData.Status(202));
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            _pipeline.Send(Clicked);
            TickAfter(TimeSpan.FromSeconds(30));

            // Assert
            _http.Requests.Should().BeEmpty();
            _storage.Files.Should().BeEmpty();
        }

        [Test]
        public async Task Send_KillSwitchOff_DropsEventsSendsNothingAndKeepsTheFile()
        {
            // Arrange
            StoreLeftovers(TelemetryTestData.Envelope("00000000-0000-0000-0000-0000000000ff"), droppedCount: 0, TelemetryTestData.Event("run_ended"));
            var leftovers = _storage.Files[TelemetryQueueStore.Path];
            _remoteConfig.Get(CoreFlags.TelemetryEnabled).Returns(false);
            _http.Respond(_ => TelemetryTestData.Status(202));
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            _pipeline.Send(Clicked);
            TickAfter(TimeSpan.FromSeconds(30));

            // Assert
            _http.Requests.Should().BeEmpty();
            _storage.Files[TelemetryQueueStore.Path].Should().Be(leftovers);
        }

        [Test]
        public async Task Grant_LeftoversOnDisk_SendsThemFirstThenSessionStartedUnderTheNewEnvelope()
        {
            // Arrange
            var previous = TelemetryTestData.Envelope("00000000-0000-0000-0000-0000000000ff");
            StoreLeftovers(previous, droppedCount: 0, TelemetryTestData.Event("run_ended"));
            _http.Respond(_ => TelemetryTestData.Status(202));

            // Act
            await _consent.GrantAsync(1, CancellationToken.None);
            _pipeline.Tick();

            // Assert
            _http.Requests.Should().HaveCount(2);
            var leftovers = Body(0);
            leftovers["envelope"]!["sessionId"]!.Value<string>().Should().Be(previous.SessionId);
            EventNames(leftovers).Should().Equal("run_ended");
            var current = Body(1);
            current["envelope"]!["sessionId"]!.Value<string>().Should().Be(_consent.SessionId);
            current["envelope"]!["installId"]!.Value<string>().Should().Be(_consent.InstallId);
            EventNames(current).Should().Equal("session_started");
        }

        [Test]
        public async Task Tick_EventQueued_SendsOnlyOnceTheFlushIntervalPassed()
        {
            // Arrange
            await StartSessionAsync();
            _pipeline.Send(Clicked);

            // Act
            TickAfter(TimeSpan.FromSeconds(14));
            var countBefore = _http.Requests.Count;
            TickAfter(TimeSpan.FromSeconds(1));

            // Assert
            countBefore.Should().Be(1);
            _http.Requests.Should().HaveCount(2);
            EventNames(Body(1)).Should().Equal("button_clicked");
        }

        [Test]
        public async Task Tick_QueueEmpty_SendsNothing()
        {
            // Arrange
            await StartSessionAsync();

            // Act
            TickAfter(TimeSpan.FromMinutes(1));

            // Assert
            _http.Requests.Should().HaveCount(1);
        }

        [Test]
        public async Task Tick_FlushSignalled_SendsAtOnce()
        {
            // Arrange
            await StartSessionAsync();
            _pipeline.Send(Clicked);

            // Act
            _flushSignal.Request();
            TickAfter(TimeSpan.FromSeconds(1));

            // Assert
            _http.Requests.Should().HaveCount(2);
        }

        [Test]
        public async Task Tick_ServerAsksToRetry_KeepsTheBatchUntilTheBackoffPassed()
        {
            // Arrange
            _http.Enqueue(TelemetryTestData.Status(503));
            await _consent.GrantAsync(1, CancellationToken.None);
            _pipeline.Tick();
            _http.Respond(_ => TelemetryTestData.Status(202));

            // Act
            TickAfter(TimeSpan.FromSeconds(14));
            var countDuringBackoff = _http.Requests.Count;
            TickAfter(TimeSpan.FromSeconds(1));

            // Assert
            countDuringBackoff.Should().Be(1);
            _http.Requests.Should().HaveCount(2);
            EventNames(Body(1)).Should().Equal("session_started");
        }

        [Test]
        public async Task Tick_BatchRejected_DropsItAndSendsTheNext()
        {
            // Arrange
            StoreLeftovers(TelemetryTestData.Envelope("00000000-0000-0000-0000-0000000000ff"), droppedCount: 0, TelemetryTestData.Event("run_ended"));
            _http.Enqueue(TelemetryTestData.Status(400));
            _http.Respond(_ => TelemetryTestData.Status(202));
            LogAssert.Expect(LogType.Error, new Regex("rejected 1 events \\(HTTP 400\\)"));
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            _pipeline.Tick();
            TickAfter(TimeSpan.FromMinutes(1));

            // Assert
            _http.Requests.Should().HaveCount(2);
            EventNames(Body(1)).Should().Equal("session_started");
        }

        [Test]
        public async Task Deny_WhileABatchIsInFlight_ClearsTheQueueDeletesTheFileAndIgnoresTheResult()
        {
            // Arrange
            await _consent.GrantAsync(1, CancellationToken.None);
            _pipeline.Send(Clicked);
            _http.Respond(_ =>
            {
                _consent.DenyAsync(1, CancellationToken.None).Forget();
                return TelemetryTestData.Status(202);
            });

            // Act
            _pipeline.Tick();
            _pipeline.Send(Clicked);
            TickAfter(TimeSpan.FromMinutes(1));

            // Assert
            _http.Requests.Should().HaveCount(1);
            _storage.Exists(TelemetryQueueStore.Path).Should().BeFalse();
        }

        [Test]
        public async Task Quit_GateOpen_SendsSessionEndedWithTheDuration()
        {
            // Arrange
            await StartSessionAsync();
            _clock.Advance(TimeSpan.FromSeconds(90));

            // Act
            await _quitHandler!(CancellationToken.None);

            // Assert
            var sessionEnded = Body(1)["events"]!.Single();
            sessionEnded["name"]!.Value<string>().Should().Be("session_ended");
            sessionEnded["props"]!["duration_s"]!.Value<int>().Should().Be(90);
        }

        [Test]
        public async Task Quit_BudgetRunsOutDuringTheSend_KeepsTheRemainderOnDisk()
        {
            // Arrange
            await _consent.GrantAsync(1, CancellationToken.None);
            using var budget = new CancellationTokenSource();
            _http.Respond(_ =>
            {
                budget.Cancel();
                return TelemetryTestData.Status(202);
            });

            // Act
            Func<Task> quit = () => _quitHandler!(budget.Token).AsTask();

            // Assert
            await quit.Should().ThrowAsync<OperationCanceledException>();
            var saved = await _store.LoadAsync(CancellationToken.None);
            saved!.Groups!.Single().Events!.Select(e => e.Name).Should().Equal("session_started", "session_ended");
        }

        [Test]
        public async Task Tick_EventQueued_WritesTheQueueFileOneSecondLater()
        {
            // Arrange
            await StartSessionAsync();
            _pipeline.Send(Clicked);
            _pipeline.Tick();

            // Act
            TickAfter(TimeSpan.FromSeconds(0.5));
            var isWrittenEarly = _storage.Exists(TelemetryQueueStore.Path);
            TickAfter(TimeSpan.FromSeconds(0.5));

            // Assert
            isWrittenEarly.Should().BeFalse();
            var saved = await _store.LoadAsync(CancellationToken.None);
            saved!.Groups!.Single().Events!.Select(e => e.Name).Should().Equal("button_clicked");
        }

        [Test]
        public async Task Tick_EventsWereDropped_SendsTelemetryDroppedAfterTheNextDelivery()
        {
            // Arrange
            StoreLeftovers(TelemetryTestData.Envelope("00000000-0000-0000-0000-0000000000ff"), droppedCount: 5, TelemetryTestData.Event("run_ended"));
            _http.Respond(_ => TelemetryTestData.Status(202));
            await _consent.GrantAsync(1, CancellationToken.None);

            // Act
            _pipeline.Tick();

            // Assert
            var current = Body(1);
            EventNames(current).Should().Equal("session_started", "telemetry_dropped");
            current["events"]![1]!["props"]!["count"]!.Value<int>().Should().Be(5);
        }

        private TelemetryPipeline CreatePipeline(TelemetryOptions options)
        {
            return CreatePipeline(options, new ReportingRule(false, Substitute.For<IReportingOverride>()));
        }

        private TelemetryPipeline CreatePipeline(TelemetryOptions options, ReportingRule reportingRule)
        {
            var pipeline = new TelemetryPipeline(options, reportingRule, _consent, _remoteConfig, _store, _http, _serializer, _clock, _application, _flushSignal);
            pipeline.Initialize();
            return pipeline;
        }

        private async Task StartSessionAsync()
        {
            _http.Respond(_ => TelemetryTestData.Status(202));
            await _consent.GrantAsync(1, CancellationToken.None);
            _pipeline.Tick();
        }

        private void TickAfter(TimeSpan delay)
        {
            _clock.Advance(delay);
            _pipeline.Tick();
        }

        private void StoreLeftovers(TelemetryEnvelope envelope, int droppedCount, params TelemetryEvent[] events)
        {
            var dto = new TelemetryQueueDto(TelemetryQueueDto.CurrentVersion, droppedCount, new[] { new TelemetryQueueGroupDto(envelope, events) });
            _storage.Files[TelemetryQueueStore.Path] = _serializer.Serialize(dto);
        }

        private JObject Body(int index)
        {
            return TelemetryTestData.ReadBody(_http.Requests[index]);
        }

        private static IEnumerable<string> EventNames(JObject body)
        {
            return body["events"]!.Select(e => e["name"]!.Value<string>()!);
        }
    }
}
