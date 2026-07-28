using System.Reflection;

namespace GsproLighting.Ui.Updates;

public static class AppVersionInfo
{
    public static string Current
    {
        get
        {
            var informational = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static bool TryParseSemVer(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var cleaned = text.Trim();
        if (cleaned.StartsWith('v') || cleaned.StartsWith('V'))
            cleaned = cleaned[1..];

        var plus = cleaned.IndexOf('+');
        if (plus >= 0)
            cleaned = cleaned[..plus];

        var dash = cleaned.IndexOf('-');
        if (dash >= 0)
            cleaned = cleaned[..dash];

        if (!Version.TryParse(cleaned, out var parsed) || parsed is null)
            return false;

        version = parsed;
        return true;
    }
}
