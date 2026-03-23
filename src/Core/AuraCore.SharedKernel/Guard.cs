namespace AuraCore.SharedKernel;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static string AgainstNullOrEmpty(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or empty.", paramName)
            : value;
}
