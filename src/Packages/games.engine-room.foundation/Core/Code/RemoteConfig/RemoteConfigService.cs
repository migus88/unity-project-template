using System;
using System.Collections.Generic;
using System.Threading;
using Core.Http;
using Core.Logging;
using Core.Results;
using Core.Storage;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Core.RemoteConfig
{
    public sealed class RemoteConfigService : IRemoteConfigService
    {
        private const long NotFoundStatus = 404;

        public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(3);

        private static readonly IReadOnlyDictionary<string, JToken> NoFlags = new Dictionary<string, JToken>();

        private IReadOnlyDictionary<string, JToken> _remote = NoFlags;
        private string _remoteOrigin = "defaults";
        private bool _hasLoaded;

        private readonly IHttpService _http;
        private readonly IFileStorage _storage;
        private readonly IJsonSerializer _serializer;
        private readonly RemoteConfigSource _source;
        private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedRemoteKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedEnvironmentKeys = new(StringComparer.Ordinal);

        public RemoteConfigService(IHttpService http, IFileStorage storage, IJsonSerializer serializer, RemoteConfigSource source)
        {
            _http = http;
            _storage = storage;
            _serializer = serializer;
            _source = source;

            foreach (var entry in source.Defaults)
            {
                _environment[entry.Key] = entry.Value;
            }
        }

        public T Get<T>(RemoteFlag<T> flag)
        {
            if (_remote.TryGetValue(flag.Key, out var token))
            {
                if (flag.TryParse(token, out var remoteValue))
                {
                    return remoteValue;
                }

                WarnOnce(_warnedRemoteKeys, flag, "remote");
            }

            if (_environment.TryGetValue(flag.Key, out var text))
            {
                if (flag.TryParse(text, out var environmentValue))
                {
                    return environmentValue;
                }

                WarnOnce(_warnedEnvironmentKeys, flag, "environment");
            }

            return flag.Default;
        }

        internal async UniTask LoadAsync(CancellationToken ct)
        {
            if (_hasLoaded)
            {
                throw new InvalidOperationException("Remote config has already been loaded.");
            }

            _hasLoaded = true;
            var cachedETag = await LoadCacheAsync(ct);

            if (string.IsNullOrWhiteSpace(_source.Endpoint))
            {
                Log.Info(LogTags.RemoteConfig, "Remote config fetch disabled: the build environment has no endpoint.");
            }
            else
            {
                var url = RemoteConfigUrl.Build(_source.Endpoint!, _source.BuildVersion, _source.Platform);
                var result = await _http.SendAsync(HttpRequest.Get(url, FetchTimeout, cachedETag), ct);
                await ApplyFetchAsync(result, ct);
            }

            _warnedRemoteKeys.Clear();
            _warnedEnvironmentKeys.Clear();
            Log.Info(LogTags.RemoteConfig, $"Remote config ready from {_remoteOrigin} ({_remote.Count} remote flags, {_environment.Count} environment defaults).");
        }

        private async UniTask<string?> LoadCacheAsync(CancellationToken ct)
        {
            var read = await _storage.ReadAsync(RemoteConfigCacheDto.FileName, ct);

            if (read.TryPickT1(out _, out var remaining))
            {
                return null;
            }

            if (remaining.TryPickT1(out var error, out var json))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config cache could not be read, ignoring it: {error.Message}");
                return null;
            }

            var deserialized = _serializer.Deserialize<RemoteConfigCacheDto>(json);

            if (deserialized.TryPickT1(out var corrupted, out var cache))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config cache is corrupted, ignoring it: {corrupted.Reason}");
                return null;
            }

            if (cache.Version != RemoteConfigCacheDto.CurrentVersion)
            {
                Log.Info(LogTags.RemoteConfig, $"Remote config cache version {cache.Version} is not {RemoteConfigCacheDto.CurrentVersion}, ignoring it.");
                return null;
            }

            if (!string.Equals(cache.Environment, _source.EnvironmentName, StringComparison.Ordinal))
            {
                Log.Info(LogTags.RemoteConfig, $"Remote config cache belongs to environment '{cache.Environment}', not '{_source.EnvironmentName}', ignoring it.");
                return null;
            }

            var document = ParseDocument(cache.Document ?? string.Empty);

            if (document.TryPickT1(out var invalid, out var flags))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config cache holds an invalid document, ignoring it: {invalid.Reason}");
                return null;
            }

            UseRemote(flags, "cache");
            return cache.ETag;
        }

        private async UniTask ApplyFetchAsync(HttpResult result, CancellationToken ct)
        {
            if (result.TryPickT0(out var completed, out var failure))
            {
                await ApplyCompletedAsync(completed.Response, ct);
                return;
            }

            if (failure.TryPickT0(out var httpError, out var transportFailure))
            {
                ApplyHttpError(httpError.Response);
                return;
            }

            var reason = transportFailure.Match(
                network => $"network error: {network.Message}",
                timeout => $"timed out after {timeout.After.TotalSeconds:0.#} s");
            Log.Warn(LogTags.RemoteConfig, $"Remote config fetch failed ({reason}); keeping the values from {_remoteOrigin}.");
        }

        private async UniTask ApplyCompletedAsync(HttpResponse response, CancellationToken ct)
        {
            if (response.IsNotModified)
            {
                Log.Info(LogTags.RemoteConfig, $"Remote config not modified; keeping the values from {_remoteOrigin}.");
                return;
            }

            var body = response.ReadText();
            var document = ParseDocument(body);

            if (document.TryPickT1(out var invalid, out var flags))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config response is invalid ({invalid.Reason}); keeping the values from {_remoteOrigin}.");
                return;
            }

            UseRemote(flags, "remote");
            var cache = new RemoteConfigCacheDto(RemoteConfigCacheDto.CurrentVersion, _source.EnvironmentName, response.ETag, body);
            var written = await _storage.WriteAsync(RemoteConfigCacheDto.FileName, _serializer.Serialize(cache), ct);

            if (written.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config cache could not be written: {error.Message}");
            }
        }

        private void ApplyHttpError(HttpResponse response)
        {
            if (response.StatusCode != NotFoundStatus)
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config fetch failed with HTTP {response.StatusCode}; keeping the values from {_remoteOrigin}.");
                return;
            }

            UseRemote(NoFlags, "defaults");
            Log.Info(LogTags.RemoteConfig, $"No remote config exists for environment '{_source.EnvironmentName}'; using defaults.");

            if (!_storage.Exists(RemoteConfigCacheDto.FileName))
            {
                return;
            }

            var deleted = _storage.Delete(RemoteConfigCacheDto.FileName);

            if (deleted.TryPickT1(out var error, out _))
            {
                Log.Warn(LogTags.RemoteConfig, $"Remote config cache could not be deleted: {error.Message}");
            }
        }

        private OneOf<IReadOnlyDictionary<string, JToken>, Corrupted> ParseDocument(string json)
        {
            var deserialized = _serializer.Deserialize<RemoteConfigDocument>(json);

            if (deserialized.TryPickT1(out var corrupted, out var document))
            {
                return corrupted;
            }

            if (document.Schema != RemoteConfigDocument.SupportedSchema)
            {
                return new Corrupted($"schema {document.Schema?.ToString() ?? "missing"} is not {RemoteConfigDocument.SupportedSchema}");
            }

            if (document.Flags == null)
            {
                return new Corrupted("flags are missing");
            }

            return OneOf<IReadOnlyDictionary<string, JToken>, Corrupted>.FromT0(document.Flags);
        }

        private void UseRemote(IReadOnlyDictionary<string, JToken> flags, string origin)
        {
            _remote = flags;
            _remoteOrigin = origin;
        }

        private static void WarnOnce<T>(HashSet<string> warnedKeys, RemoteFlag<T> flag, string layer)
        {
            if (warnedKeys.Add(flag.Key))
            {
                Log.Warn(LogTags.RemoteConfig, $"The {layer} value of '{flag.Key}' is not a valid {flag.KindName}; using a lower layer.");
            }
        }
    }
}
