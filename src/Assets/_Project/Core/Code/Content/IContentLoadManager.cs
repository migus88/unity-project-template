using Unity.Loading;

namespace Core.Content
{
    public interface IContentLoadManager
    {
        ContentDirectoryHandle RegisterContentDirectory(string path);
        T[] GetRootAssets<T>(ContentDirectoryHandle handle) where T : UnityEngine.Object;
    }
}
