using Core.Domains;
using Core.Results;
using OneOf;
using Unity.Loading;

namespace Core.Content
{
    public interface IContentDirectoryRegistry
    {
        OneOf<ContentDirectoryHandle, NotFound> Get(string name);
        OneOf<DomainContent, NotFound> GetContent(DomainDescriptor descriptor);
    }
}
