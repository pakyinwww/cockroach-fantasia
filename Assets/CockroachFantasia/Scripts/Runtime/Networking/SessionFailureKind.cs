namespace CockroachFantasia.Networking
{
    public enum SessionFailureKind
    {
        None,
        InvalidCode,
        Expired,
        Full,
        Locked,
        Unreachable,
        HostLeft,
        Disconnected,
        Unauthorized,
        Unknown
    }
}
