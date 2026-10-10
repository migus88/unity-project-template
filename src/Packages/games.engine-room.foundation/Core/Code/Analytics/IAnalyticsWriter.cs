namespace Core.Analytics
{
    public interface IAnalyticsWriter
    {
        void Add(string key, string value);

        void Add(string key, int value);

        void Add(string key, float value);

        void Add(string key, bool value);
    }
}
