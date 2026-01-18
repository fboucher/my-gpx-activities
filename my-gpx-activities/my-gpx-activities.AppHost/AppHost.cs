var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.my_gpx_activities_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.webapp>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
