using System.Reflection;

namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Exposes the SDK assembly version. Used by the host to verify that a plugin
/// was compiled against a compatible SDK version (major.minor must match).
/// The version is set automatically from the .csproj <c>&lt;Version&gt;</c> property.
/// </summary>
public static class SdkInfo
{
    /// <summary>The SDK version in "major.minor.patch" format (e.g., "1.0.0").</summary>
    public static readonly string Version =
        typeof(SdkInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
