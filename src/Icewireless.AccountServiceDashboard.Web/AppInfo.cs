using System.Reflection;

namespace Icewireless.AccountServiceDashboard.Web;

public static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var raw = string.IsNullOrWhiteSpace(informational)
            ? typeof(Program).Assembly.GetName().Version?.ToString(3)
            : informational;
        if (string.IsNullOrWhiteSpace(raw))
            return "1.1.2";
        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
