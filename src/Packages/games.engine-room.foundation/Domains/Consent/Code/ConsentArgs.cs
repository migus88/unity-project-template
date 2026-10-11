namespace Consent
{
    public sealed record ConsentArgs
    {
#if UNITY_EDITOR
        public static ConsentArgs CreateDebug() => new();
#endif
    }
}
