using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core.Http;
using Core.RemoteConfig;
using Core.Storage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TestUtils;
using UnityEngine;
using UnityEngine.TestTools;

namespace Core.Tests.RemoteConfig
{
    public sealed class RemoteConfigServiceTests
    {
        private const string Endpoint = "https://example.test";
        private const string EnvironmentName = "staging";
        private const string CacheFile = RemoteConfigCacheDto.FileName;

        private static readonly IntFlag Limit = new("LimitValue", 1);
        private static readonly BoolFlag Enabled = new("enabled", false);

        private FakeHttpService _http = null!;
        private InMemoryFileStorage _storage = null!;
        private JsonSerializer _serializer = null!;

        [SetUp]
        public void SetUp()
        {
            _http = new FakeHttpService();
            _storage = new InMemoryFileStorage();
            _serializer = new JsonSerializer();
        }

        [Test]
        public void Get_NotLoaded_ReturnsCompiledDefault()
        {
            // Arrange
            var service = CreateService();

            // Act
            var value = service.Get(Limit);

            // Assert
            value.Should().Be(1);
        }

        [Test]
        public void Get_EnvironmentDefault_OverridesCompiledDefault()
        {
            // Arrange
            var service = CreateService(new KeyValuePair<string, string>(Limit.Key, "5"));

            // Act
            var value = service.Get(Limit);

            // Assert
            value.Should().Be(5);
        }

        [Test]
        public void Get_EnvironmentDefaultUnparsable_FallsBackToCompiledDefaultAndWarnsOnce()
        {
            // Arrange
            var service = CreateService(new KeyValuePair<string, string>(Limit.Key, "five"));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] The environment value of 'LimitValue' is not a valid int; using a lower layer.");

            // Act
            service.Get(Limit);
            var value = service.Get(Limit);

            // Assert
            value.Should().Be(1);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task LoadAsync_Fetched200_UsesRemoteValueAndWritesCache()
        {
            // Arrange
            var service = CreateService();
            var body = Document("{\"LimitValue\": 9}");
            _http.Enqueue(Ok(body, "\"v1\""));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            service.Get(Limit).Should().Be(9);
            var cache = _serializer.Deserialize<RemoteConfigCacheDto>(_storage.Files[CacheFile]);
            cache.TryPickT0(out var dto, out _).Should().BeTrue();
            dto.Should().Be(new RemoteConfigCacheDto(RemoteConfigCacheDto.CurrentVersion, EnvironmentName, "\"v1\"", body));
        }

        [Test]
        public async Task LoadAsync_RemoteValueWrongType_FallsBackToEnvironmentLayer()
        {
            // Arrange
            var service = CreateService(new KeyValuePair<string, string>(Limit.Key, "5"));
            _http.Enqueue(Ok(Document("{\"LimitValue\": \"9\"}"), null));
            await service.LoadAsync(CancellationToken.None);
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] The remote value of 'LimitValue' is not a valid int; using a lower layer.");

            // Act
            var value = service.Get(Limit);

            // Assert
            value.Should().Be(5);
        }

        [Test]
        public async Task LoadAsync_CachedAndNotModified_SendsEtagAndUsesCache()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(Response(304, string.Empty));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests[0].Headers.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>(HttpRequest.IfNoneMatchHeader, "\"v1\""));
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_Timeout_UsesCache()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(new HttpResult.Timeout(RemoteConfigService.FetchTimeout));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] Remote config fetch failed (timed out after 3 s); keeping the values from cache.");

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests[0].Timeout.Should().Be(TimeSpan.FromSeconds(3));
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_NetworkErrorWithoutCache_UsesDefaults()
        {
            // Arrange
            var service = CreateService();
            _http.Enqueue(new HttpResult.NetworkError("offline"));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] Remote config fetch failed (network error: offline); keeping the values from defaults.");

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests[0].Headers.Should().BeEmpty();
            service.Get(Limit).Should().Be(1);
        }

        [Test]
        public async Task LoadAsync_NotFound_DeletesCacheAndUsesDefaults()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(new HttpResult.HttpError(Response(404, string.Empty).Response));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _storage.Files.Should().NotContainKey(CacheFile);
            service.Get(Limit).Should().Be(1);
        }

        [Test]
        public async Task LoadAsync_ServerError_KeepsCache()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(new HttpResult.HttpError(Response(503, string.Empty).Response));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] Remote config fetch failed with HTTP 503; keeping the values from cache.");

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _storage.Files.Should().ContainKey(CacheFile);
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_CorruptBody_KeepsCacheUntouched()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var cachedJson = _storage.Files[CacheFile];
            var service = CreateService();
            _http.Enqueue(Ok("{not json", "\"v2\""));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RemoteConfig\] Remote config response is invalid"));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _storage.Files[CacheFile].Should().Be(cachedJson);
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_UnsupportedSchema_KeepsCache()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(Ok("{\"schema\": 2, \"flags\": {\"LimitValue\": 9}}", "\"v2\""));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] Remote config response is invalid (schema 2 is not 1); keeping the values from cache.");

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_CacheFromOtherEnvironment_IsIgnored()
        {
            // Arrange
            WriteCache("production", "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = CreateService();
            _http.Enqueue(new HttpResult.NetworkError("offline"));
            LogAssert.Expect(LogType.Warning, "[RemoteConfig] Remote config fetch failed (network error: offline); keeping the values from defaults.");

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests[0].Headers.Should().BeEmpty();
            service.Get(Limit).Should().Be(1);
        }

        [Test]
        public async Task LoadAsync_CorruptCacheFile_FetchesWithoutEtag()
        {
            // Arrange
            _storage.Files[CacheFile] = "{broken";
            var service = CreateService();
            _http.Enqueue(Ok(Document("{\"enabled\": true}"), null));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RemoteConfig\] Remote config cache is corrupted"));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests[0].Headers.Should().BeEmpty();
            service.Get(Enabled).Should().BeTrue();
        }

        [Test]
        public async Task LoadAsync_NoEndpoint_SendsNothingAndUsesCache()
        {
            // Arrange
            WriteCache(EnvironmentName, "\"v1\"", Document("{\"LimitValue\": 4}"));
            var service = new RemoteConfigService(_http, _storage, _serializer, new RemoteConfigSource(string.Empty, EnvironmentName, Array.Empty<KeyValuePair<string, string>>(), "1.0", "OSXEditor"));

            // Act
            await service.LoadAsync(CancellationToken.None);

            // Assert
            _http.Requests.Should().BeEmpty();
            service.Get(Limit).Should().Be(4);
        }

        [Test]
        public async Task LoadAsync_CallerCancels_Throws()
        {
            // Arrange
            var service = CreateService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = () => service.LoadAsync(cts.Token).AsTask();

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task LoadAsync_SecondCall_Throws()
        {
            // Arrange
            var service = CreateService();
            _http.Enqueue(Ok(Document("{}"), null));
            await service.LoadAsync(CancellationToken.None);

            // Act
            Func<Task> act = () => service.LoadAsync(CancellationToken.None).AsTask();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        private RemoteConfigService CreateService(params KeyValuePair<string, string>[] defaults)
        {
            return new RemoteConfigService(_http, _storage, _serializer, new RemoteConfigSource(Endpoint, EnvironmentName, defaults, "1.0", "OSXEditor"));
        }

        private void WriteCache(string environment, string etag, string document)
        {
            _storage.Files[CacheFile] = _serializer.Serialize(new RemoteConfigCacheDto(RemoteConfigCacheDto.CurrentVersion, environment, etag, document));
        }

        private static string Document(string flags)
        {
            return $"{{\"schema\": 1, \"flags\": {flags}}}";
        }

        private static HttpResult.Completed Ok(string body, string? etag)
        {
            var headers = new Dictionary<string, string>();

            if (etag != null)
            {
                headers["ETag"] = etag;
            }

            return new HttpResult.Completed(new HttpResponse(200, Encoding.UTF8.GetBytes(body), headers));
        }

        private static HttpResult.Completed Response(long status, string body)
        {
            return new HttpResult.Completed(new HttpResponse(status, Encoding.UTF8.GetBytes(body), new Dictionary<string, string>()));
        }
    }
}
