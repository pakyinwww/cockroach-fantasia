namespace CockroachFantasia.Networking
{
    public enum SessionConnectionState
    {
        Idle,
        Connecting,
        Connected,
        Leaving,
        Failed
    }
}
