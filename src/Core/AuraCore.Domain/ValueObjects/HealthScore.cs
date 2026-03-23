namespace AuraCore.Domain.ValueObjects;

public readonly record struct HealthScore(int Value)
{
    public int Value { get; } = Value is >= 0 and <= 100
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), "Score must be 0-100.");
}
