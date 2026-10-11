namespace Core.Analytics
{
    public sealed class AnalyticsFlushSignal
    {
        private bool _isRequested;

        public void Request()
        {
            _isRequested = true;
        }

        internal bool TryTake()
        {
            if (!_isRequested)
            {
                return false;
            }

            _isRequested = false;
            return true;
        }
    }
}
