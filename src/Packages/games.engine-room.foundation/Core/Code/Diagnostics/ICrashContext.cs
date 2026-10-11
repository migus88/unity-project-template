namespace Core.Diagnostics
{
    public interface ICrashContext
    {
        void SetSessionFile(string storagePath);

        void ClearSessionFile();
    }
}
