using System;
using Core.Results;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Core.Settings
{
    public sealed record SettingsSection<T>(string Key, int CurrentVersion, Func<JObject, int, OneOf<JObject, Corrupted>> Migrate, T Default) where T : class;
}
