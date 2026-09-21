namespace Darkspyre.Persistence;

internal static class LogicalName
{
    internal const string InternalDirectoryName = ".darkspyre-persistence";

    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    public static string StoreName(string storeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ValidateSegment(storeName, nameof(storeName));
        return storeName;
    }

    public static string AssetName(string assetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        ValidateAssetPath(assetName, nameof(assetName), allowTrailingSeparator: false);
        return assetName;
    }

    public static string AssetNamePrefix(string assetNamePrefix)
    {
        ArgumentNullException.ThrowIfNull(assetNamePrefix);
        if (assetNamePrefix.Length == 0)
        {
            return assetNamePrefix;
        }

        if (string.IsNullOrWhiteSpace(assetNamePrefix))
        {
            throw new ArgumentException("The asset-name prefix cannot contain only whitespace.", nameof(assetNamePrefix));
        }

        ValidateAssetPath(assetNamePrefix, nameof(assetNamePrefix), allowTrailingSeparator: true);
        return assetNamePrefix;
    }

    private static void ValidateAssetPath(string value, string parameterName, bool allowTrailingSeparator)
    {
        if (value[0] == '/' || value.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Asset names must be relative logical names that use '/' as their separator.",
                parameterName);
        }

        string[] segments = value.Split('/');
        int segmentCount = segments.Length;
        if (allowTrailingSeparator && segments[^1].Length == 0)
        {
            segmentCount--;
        }

        if (segmentCount == 0)
        {
            throw new ArgumentException("The asset name must contain at least one segment.", parameterName);
        }

        for (int index = 0; index < segmentCount; index++)
        {
            ValidateSegment(segments[index], parameterName);
            if (string.Equals(segments[index], InternalDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The asset name uses a directory reserved by the persistence provider.", parameterName);
            }
        }

        for (int index = segmentCount; index < segments.Length; index++)
        {
            if (segments[index].Length != 0 || index != segments.Length - 1)
            {
                throw new ArgumentException("Asset names cannot contain empty path segments.", parameterName);
            }
        }
    }

    private static void ValidateSegment(string segment, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
        {
            throw new ArgumentException("Logical-name segments cannot be empty, whitespace, '.' or '..'.", parameterName);
        }

        if (!string.Equals(segment, segment.Trim(), StringComparison.Ordinal) || segment.EndsWith('.'))
        {
            throw new ArgumentException("Logical-name segments cannot begin or end with whitespace or end with '.'.", parameterName);
        }

        if (segment.Contains('/') ||
            segment.Any(IsInvalidPortableCharacter) ||
            segment.Any(char.IsControl))
        {
            throw new ArgumentException("The logical name contains characters that are not portable filesystem names.", parameterName);
        }

        string deviceName = segment.Split('.', 2)[0];
        if (WindowsReservedNames.Contains(deviceName))
        {
            throw new ArgumentException("The logical name contains an operating-system-reserved name.", parameterName);
        }
    }

    private static bool IsInvalidPortableCharacter(char value) =>
        value is '<' or '>' or ':' or '"' or '\\' or '|' or '?' or '*' or '\0';
}
