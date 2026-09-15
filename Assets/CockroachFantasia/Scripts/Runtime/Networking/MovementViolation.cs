namespace CockroachFantasia.Networking
{
    public enum MovementViolation : byte
    {
        None,
        OutOfBounds,
        Teleport,
        SustainedSpeed
    }
}
