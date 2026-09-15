namespace CockroachFantasia.Gameplay
{
    public interface IRandomSource
    {
        int Range(int minimumInclusive, int maximumExclusive);
        float Value { get; }
    }
}
