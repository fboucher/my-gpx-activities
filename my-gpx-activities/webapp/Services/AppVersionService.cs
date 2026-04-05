using System.Reflection;

namespace webapp.Services;

public class AppVersionService : IAppVersionService
{
    public AppVersionInfo GetVersionInfo()
    {
        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        string version;
        string build;

        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = informationalVersion[..plusIndex];
            build = informationalVersion[(plusIndex + 1)..];
        }
        else
        {
            version = informationalVersion;
            build = "dev";
        }

        var runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID");
        if (!string.IsNullOrEmpty(runId))
        {
            build = runId[..Math.Min(10, runId.Length)];
        }

        return new AppVersionInfo(version, build);
    }
}
