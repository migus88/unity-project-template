namespace Core.Platform
{
    public sealed class NullPlatformPresence : IPlatformPresence
    {
        public void Set(Presence presence)
        {
        }

        public void Clear()
        {
        }
    }
}
