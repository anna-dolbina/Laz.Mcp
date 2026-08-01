using ModelContextProtocol;

namespace Laz.Mcp;

/// <summary>
/// Parses enum-like string tool parameters (<c>key</c>, <c>button</c>, <c>path</c>, <c>easing</c>)
/// case-insensitively, throwing a legible <see cref="McpException"/> listing valid values on
/// failure instead of relying on default JSON-schema enum-as-integer serialization.
/// </summary>
internal static class EnumParsing
{
    public static T Parse<T>(string value, string paramName) where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result))
        {
            return result;
        }

        var validValues = string.Join(", ", Enum.GetNames<T>());
        throw new McpException(
            $"Invalid value '{value}' for parameter '{paramName}'. Valid values are: {validValues}.");
    }
}
