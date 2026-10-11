namespace Core.Analytics
{
    public sealed class NullUiClickTracker : IUiClickTracker
    {
        public void Track(UiClick click)
        {
        }
    }
}
