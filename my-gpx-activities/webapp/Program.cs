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

// Configure HttpClient for API service
builder.Services.AddHttpClient("ApiService", (serviceProvider, client) =>
{
    // Get the API service endpoint from configuration
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var apiServiceUrl = configuration["services:apiservice:http:0"];
    if (!string.IsNullOrEmpty(apiServiceUrl))
    {
        client.BaseAddress = new Uri(apiServiceUrl);
    }
});

// Add MudBlazor services
builder.Services.AddMudServices();

// Add activity store (in-memory for now, will be replaced with database)
builder.Services.AddSingleton<ActivityStore>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
