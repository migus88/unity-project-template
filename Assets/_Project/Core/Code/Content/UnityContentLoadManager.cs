using Unity.Loading;

namespace Core.Content
{
    public sealed class UnityContentLoadManager : IContentLoadManager
    {
        public ContentDirectoryHandle RegisterContentDirectory(string path)
        {
            return ContentLoadManager.RegisterContentDirectory(path);
        }

        public T[] GetRootAssets<T>(ContentDirectoryHandle handle) where T : UnityEngine.Object
        {
            return ContentLoadManager.GetRootAssets<T>(handle);
        }
    }
}
