namespace webapp.Services;

public record AppVersionInfo(string Version, string Build)
{
    public string Display => $"Version: {Version}+{Build}";
}
