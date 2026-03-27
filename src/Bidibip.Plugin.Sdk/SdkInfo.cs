using System.Reflection;

namespace Bidibip.Plugin.Sdk;

public static class SdkInfo
{
    public static readonly string Version =
        typeof(SdkInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
