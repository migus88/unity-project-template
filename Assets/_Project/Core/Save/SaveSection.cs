using System;
using Core.Results;
using Newtonsoft.Json.Linq;
using OneOf;

namespace Core.Save
{
    public sealed record SaveSection<T>(string Key, int CurrentVersion, Func<JObject, int, OneOf<JObject, Corrupted>> Migrate) where T : class;
}
