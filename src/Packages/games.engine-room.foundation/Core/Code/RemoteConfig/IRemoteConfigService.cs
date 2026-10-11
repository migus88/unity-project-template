namespace Core.RemoteConfig
{
    public interface IRemoteConfigService
    {
        T Get<T>(RemoteFlag<T> flag);
    }
}
