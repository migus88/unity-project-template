using Core.Results;
using OneOf;

namespace Core.Storage
{
    public interface IJsonSerializer
    {
        string Serialize<T>(T value);
        OneOf<T, Corrupted> Deserialize<T>(string json);
    }
}
