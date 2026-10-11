namespace Core.Platform
{
    public interface IPlatformPresence
    {
        void Set(Presence presence);

        void Clear();
    }
}
