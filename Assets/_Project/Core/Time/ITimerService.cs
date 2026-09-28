namespace Core.Time
{
    public interface ITimerService
    {
        ITickSource Real { get; }
        ITickSource Game { get; }
    }
}
