using System.IO;
using System.Text.RegularExpressions;

namespace AuraCore.PrivilegedService.Ops;

public static class ArgumentValidator
{
    private static readonly char[] ShellMetaChars = { ';', '|', '&', '`', '$', '>', '<', '\n', '\r', '\0' };
    private static readonly Regex ServiceNameRegex = new(@"^[A-Za-z0-9_\-\.]{1,256}$", RegexOptions.Compiled);
    private static readonly Regex SubstitutionRegex = new(@"\$\(|\$\{", RegexOptions.Compiled);

    public static bool IsSafeArgument(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value.IndexOfAny(ShellMetaChars) >= 0) return false;
        if (SubstitutionRegex.IsMatch(value)) return false;
        return true;
    }

    public static bool IsPathUnderPrefix(string path, string prefix)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(prefix)) return false;

        try
        {
            var full = Path.GetFullPath(path);
            var fullPrefix = Path.GetFullPath(prefix);
            if (!fullPrefix.EndsWith(Path.DirectorySeparatorChar))
                fullPrefix += Path.DirectorySeparatorChar;
            return full.StartsWith(fullPrefix, System.StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidServiceName(string? name)
        => !string.IsNullOrEmpty(name) && ServiceNameRegex.IsMatch(name);
}
