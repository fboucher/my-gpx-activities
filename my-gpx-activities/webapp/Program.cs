using MudBlazor.Services;
using webapp.Components;
using webapp.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel file upload limits (10MB for GPX files)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Configure HttpClient for API service with extended timeout for file uploads
builder.Services.AddHttpClient("ApiService", (serviceProvider, client) =>
{
    // Get the API service endpoint from configuration
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var apiServiceUrl = configuration["services:apiservice:http:0"];
    if (!string.IsNullOrEmpty(apiServiceUrl))
    {
        client.BaseAddress = new Uri(apiServiceUrl);
    }
    // Set longer timeout for file uploads (2 minutes)
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Add MudBlazor services
builder.Services.AddMudServices();

// Add activity store (in-memory for now, will be replaced with database)
builder.Services.AddSingleton<ActivityStore>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure SignalR for file uploads with extended timeouts
builder.Services.AddSignalR(options =>
{
    // Increase maximum message size for file uploads (100MB to handle multiple files)
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024;
    
    // Disable timeout while streaming file data (set to null for no timeout)
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    
    // Keep-alive interval
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    
    // Handshake timeout
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
});

// Configure Blazor Server Circuit options for file uploads
builder.Services.AddServerSideBlazor(options =>
{
    // Increase the disconnect timeout to prevent disconnection during file uploads
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    
    // Allow more time for synchronous JS interop during file reads
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
    
    // Increase the maximum buffer size for receiving data
    options.DetailedErrors = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
