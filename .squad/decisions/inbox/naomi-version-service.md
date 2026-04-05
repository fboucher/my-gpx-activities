# Decision: Version info service lives in webapp

**Date:** 2025-06-13  
**Author:** Naomi  
**Issue:** #39

## Decision
`AppVersionService` and its interface live in `my-gpx-activities/webapp/Services/`, not in a shared library. Version display is a UI concern and the webapp is the only consumer.

## Interface contract (agreed with Alex)
```csharp
public record AppVersionInfo(string Version, string Build)
{
    public string Display => $"Version: {Version}+{Build}";
}

public interface IAppVersionService
{
    AppVersionInfo GetVersionInfo();
}
```

## Build value resolution order
1. If `GITHUB_RUN_ID` env var is set → use first 10 chars (CI/CD)
2. Else if `AssemblyInformationalVersion` contains `+` suffix → use that suffix
3. Else → `"dev"`

## Registration
Singleton in `webapp/Program.cs`:
```csharp
builder.Services.AddSingleton<IAppVersionService, AppVersionService>();
```
