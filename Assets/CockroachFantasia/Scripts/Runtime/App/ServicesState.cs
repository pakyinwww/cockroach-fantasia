namespace CockroachFantasia.App
{
    public enum ServicesState
    {
        NotStarted,
        InitializingServices,
        SigningIn,
        Ready,
        Failed,
        Cancelled
    }

    public enum ServicesErrorKind
    {
        None,
        Offline,
        TimedOut,
        Cancelled,
        Configuration,
        Authentication,
        Unknown
    }
}
