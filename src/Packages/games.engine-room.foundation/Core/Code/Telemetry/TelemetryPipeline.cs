using System;
using System.Text;
using System.Threading;
using Core.Analytics;
using Core.Logging;
using Core.Privacy;
using Core.RemoteConfig;
using Core.Http;
using Core.Storage;
using Core.Time;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace Core.Telemetry
{
    internal sealed class TelemetryPipeline : IAnalyticsBackend, IInitializable, ITickable, IDisposable
    {
        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private long _seq;
        private bool _wasGranted;
        private bool _isQueueLoaded;
        private bool _isFlushing;
        private bool _isSaving;
        private bool _isQuitting;
        private bool _isFlushRequested;
        private DateTime _sessionStartedAt;
        private DateTime _lastFlushAt = DateTime.MinValue;
        private DateTime? _dirtySince;
        private TelemetryEnvelope? _envelope;
        private CancellationTokenSource _gateCts = new();
        private IDisposable? _quitRegistration;
        private DisposableBag _disposables;

        private readonly TelemetryOptions _options;
        private readonly IConsentService _consent;
        private readonly IRemoteConfigService _remoteConfig;
        private readonly TelemetryQueueStore _store;
        private readonly IHttpService _http;
        private readonly IJsonSerializer _serializer;
        private readonly IRealClock _clock;
        private readonly IApplicationService _application;
        private readonly AnalyticsFlushSignal _flushSignal;
        private readonly TelemetryQueue _queue = new();
        private readonly JsonAnalyticsWriter _writer = new();
        private readonly TelemetryBackoff _backoff = new();

        public TelemetryPipeline(
            TelemetryOptions options,
            IConsentService consent,
            IRemoteConfigService remoteConfig,
            TelemetryQueueStore store,
            IHttpService http,
            IJsonSerializer serializer,
            IRealClock clock,
            IApplicationService application,
            AnalyticsFlushSignal flushSignal)
        {
            _options = options;
            _consent = consent;
            _remoteConfig = remoteConfig;
            _store = store;
            _http = http;
            _serializer = serializer;
            _clock = clock;
            _application = application;
            _flushSignal = flushSignal;
        }

        public void Initialize()
        {
            _consent.Current.Subscribe(OnConsentChanged).AddTo(ref _disposables);
            _quitRegistration = _application.AddQuitHandler(OnQuitAsync);
        }

        public void Send<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
            if (IsGateOpen())
            {
                Enqueue(analyticsEvent);
            }
        }

        public void Tick()
        {
            var isSignalled = _flushSignal.TryTake();

            if (!IsGateOpen() || _isQuitting)
            {
                return;
            }

            var now = _clock.UtcNow;
            _isFlushRequested |= isSignalled;
            SaveWhenDue(now);

            if (_isFlushing || _queue.Count == 0)
            {
                return;
            }

            if (!_backoff.CanSend(now))
            {
                _isFlushRequested = false;
                return;
            }

            if (_isFlushRequested || now - _lastFlushAt >= TelemetryLimits.FlushInterval)
            {
                FlushAndForgetAsync(_gateCts.Token).Forget();
            }
        }

        private bool IsGateOpen()
        {
            return _options.IsAllowed && _envelope != null && _remoteConfig.Get(CoreFlags.TelemetryEnabled);
        }

        private void OnConsentChanged(ConsentSnapshot snapshot)
        {
            if (snapshot.State == ConsentState.Granted && snapshot.InstallId != null)
            {
                Grant(snapshot);
                return;
            }

            if (snapshot.State == ConsentState.Denied || _wasGranted)
            {
                Withdraw();
            }
        }

        private void Grant(ConsentSnapshot snapshot)
        {
            if (!_options.IsAllowed)
            {
                return;
            }

            _envelope = new TelemetryEnvelope(snapshot.InstallId!, _consent.SessionId, _options.Build, _options.Platform, _options.EnvironmentName, snapshot.PolicyVersion ?? 0);

            if (_wasGranted)
            {
                return;
            }

            _wasGranted = true;
            StartSessionAsync(_gateCts.Token).Forget();
        }

        private void Withdraw()
        {
            _wasGranted = false;
            _envelope = null;
            _gateCts.Cancel();
            _gateCts.Dispose();
            _gateCts = new CancellationTokenSource();
            _queue.Clear();
            _dirtySince = null;
            _isFlushRequested = false;
            _backoff.Reset();
            _store.Delete();
        }

        private async UniTaskVoid StartSessionAsync(CancellationToken ct)
        {
            if (!_isQueueLoaded)
            {
                var leftovers = await _store.LoadAsync(ct);

                if (ct.IsCancellationRequested)
                {
                    return;
                }

                if (leftovers != null)
                {
                    _queue.Restore(leftovers);
                }

                _isQueueLoaded = true;
            }

            _sessionStartedAt = _clock.UtcNow;

            if (IsGateOpen())
            {
                Enqueue(new SessionStartedEvent());
                _isFlushRequested = true;
            }
        }

        private void Enqueue<TEvent>(TEvent analyticsEvent) where TEvent : struct, IAnalyticsEvent
        {
            var telemetryEvent = _writer.Write(analyticsEvent, _seq, ToUnixMilliseconds(_clock.UtcNow));

            if (telemetryEvent == null)
            {
                return;
            }

            _seq++;
            _queue.Enqueue(_envelope!, telemetryEvent);
        }

        private void SaveWhenDue(DateTime now)
        {
            if (!_isQueueLoaded || !_queue.IsDirty || _isSaving || _isFlushing)
            {
                return;
            }

            _dirtySince ??= now;

            if (now - _dirtySince.Value >= TelemetryLimits.SaveDelay)
            {
                SaveAndForgetAsync(_gateCts.Token).Forget();
            }
        }

        private async UniTaskVoid SaveAndForgetAsync(CancellationToken ct)
        {
            await SaveAsync(ct);
        }

        private async UniTask SaveAsync(CancellationToken ct)
        {
            if (!_isQueueLoaded)
            {
                return;
            }

            _dirtySince = null;

            if (_queue.Count == 0 && _queue.DroppedCount == 0)
            {
                _queue.TakeSnapshot();
                _store.Delete();
                return;
            }

            _isSaving = true;

            try
            {
                var isSaved = await _store.SaveAsync(_queue.TakeSnapshot(), CancellationToken.None);

                if (ct.IsCancellationRequested)
                {
                    _store.Delete();
                }
                else if (!isSaved)
                {
                    _queue.MarkDirty();
                }
            }
            finally
            {
                _isSaving = false;
            }
        }

        private async UniTaskVoid FlushAndForgetAsync(CancellationToken ct)
        {
            try
            {
                await FlushAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
        }

        private async UniTask FlushAsync(CancellationToken ct)
        {
            _isFlushing = true;
            _isFlushRequested = false;
            _lastFlushAt = _clock.UtcNow;

            try
            {
                await SaveAsync(ct);

                while (!ct.IsCancellationRequested && _queue.PeekBatch() is { } batch)
                {
                    if (!await SendBatchAsync(batch, ct))
                    {
                        break;
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    await SaveAsync(ct);
                }
            }
            finally
            {
                _isFlushing = false;
            }
        }

        private async UniTask<bool> SendBatchAsync(TelemetryBatch batch, CancellationToken ct)
        {
            var json = TelemetryBatchRequest.ToJson(batch, _serializer);

            while (batch.Events.Count > 1 && Encoding.UTF8.GetByteCount(json) > TelemetryLimits.MaxBatchBytes)
            {
                batch = batch with { Events = Take(batch, batch.Events.Count / 2) };
                json = TelemetryBatchRequest.ToJson(batch, _serializer);
            }

            var request = TelemetryBatchRequest.Create(_options, json, TelemetryLimits.RequestTimeout);
            var result = await _http.SendAsync(request, ct);
            ct.ThrowIfCancellationRequested();

            return TelemetryResponseClassifier.Classify(result).Match(
                _ => OnSent(batch),
                rejected => OnRejected(batch, rejected),
                retry => OnRetry(retry));
        }

        private bool OnSent(TelemetryBatch batch)
        {
            _queue.Remove(batch);
            _backoff.Reset();
            var dropped = _queue.TakeDroppedCount();

            if (dropped > 0)
            {
                Enqueue(new TelemetryDroppedEvent(dropped));
            }

            return true;
        }

        private bool OnRejected(TelemetryBatch batch, TelemetrySendOutcome.Rejected rejected)
        {
            Log.Error(LogTags.Telemetry, $"The server rejected {batch.Events.Count} events (HTTP {rejected.Status}): {rejected.Body}. They are dropped.");
            _queue.Remove(batch);
            return true;
        }

        private bool OnRetry(TelemetrySendOutcome.Retry retry)
        {
            _backoff.Fail(_clock.UtcNow, retry.RetryAfter);
            Log.Info(LogTags.Telemetry, $"Sending events failed ({retry.Reason}); retrying later.");
            return false;
        }

        private async UniTask OnQuitAsync(CancellationToken ct)
        {
            if (!IsGateOpen())
            {
                return;
            }

            _isQuitting = true;
            Enqueue(new SessionEndedEvent((int)(_clock.UtcNow - _sessionStartedAt).TotalSeconds));
            using var quitCts = CancellationTokenSource.CreateLinkedTokenSource(_gateCts.Token, ct);

            try
            {
                if (_isFlushing || _isSaving)
                {
                    await UniTask.WaitWhile(() => _isFlushing || _isSaving, cancellationToken: quitCts.Token);
                }

                await FlushAsync(quitCts.Token);
            }
            finally
            {
                if (!_gateCts.IsCancellationRequested && !_isSaving)
                {
                    await SaveAsync(CancellationToken.None);
                }
            }
        }

        private static TelemetryEvent[] Take(TelemetryBatch batch, int count)
        {
            var events = new TelemetryEvent[count];

            for (var i = 0; i < count; i++)
            {
                events[i] = batch.Events[i];
            }

            return events;
        }

        private static long ToUnixMilliseconds(DateTime utcNow)
        {
            return (long)(utcNow - UnixEpoch).TotalMilliseconds;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _quitRegistration?.Dispose();
            _gateCts.Cancel();
            _gateCts.Dispose();
        }
    }
}
