using Core.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneOf;

namespace Core.Storage
{
    public sealed class JsonSerializer : IJsonSerializer
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                },
            },
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.None,
            Formatting = Formatting.Indented,
        };

        public string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        public OneOf<T, Corrupted> Deserialize<T>(string json)
        {
            T? value;

            try
            {
                value = JsonConvert.DeserializeObject<T>(json, Settings);
            }
            catch (JsonException exception)
            {
                return new Corrupted(exception.Message);
            }

            if (value is null)
            {
                return new Corrupted("JSON is empty or null.");
            }

            return OneOf<T, Corrupted>.FromT0(value);
        }
    }
}
