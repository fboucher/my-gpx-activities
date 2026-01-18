# AGENTS.md - Development Guidelines for my-gpx-activities

## Overview
This is a .NET 10 Aspire application for managing personal GPX files. Users can upload, view, and analyze their GPS activity data including routes on interactive maps and detailed analytics such as average speed, maximum speed, duration, distance, and elevation data.

The application consists of multiple projects:
- **AppHost**: Aspire application host for orchestration
- **ApiService**: Minimal API service with OpenAPI/Swagger for handling GPX file processing and analytics
- **webapp**: Blazor Server application built with MudBlazor UI library for the interactive web interface
- **ServiceDefaults**: Shared service configurations and extensions
- **Tests**: NUnit-based integration tests

## Build, Lint, and Test Commands

### Building
```bash
# Build all projects
dotnet build

# Build specific project
dotnet build my-gpx-activities.ApiService

# Build in release mode
dotnet build --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test my-gpx-activities.Tests

# Run a specific test
dotnet test --filter "GetWebResourceRootReturnsOkStatusCode"

# List all available tests
dotnet test --list-tests

# Run tests with detailed output
dotnet test --verbosity detailed

# Run tests in parallel
dotnet test --parallel
```

### Linting and Code Analysis
```bash
# Run analyzers (NUnit analyzers included in test project)
dotnet build --no-restore /p:RunAnalyzersDuringBuild=true

# Check for code issues
dotnet build /warnaserror
```

### Development Server
```bash
# Run the Aspire application (includes all services)
dotnet run --project my-gpx-activities.AppHost

# Run API service individually
dotnet run --project my-gpx-activities.ApiService

# Run web application individually
dotnet run --project webapp
```

## Code Style Guidelines

### General C# Conventions

#### Project Structure
- Use PascalCase for namespace names (e.g., `webapp`)
- Organize code into logical folders mirroring namespace structure
- Place shared extensions and utilities in appropriate projects

#### Naming Conventions
- **Classes/Records**: PascalCase (e.g., `WeatherForecast`, `WeatherApiClient`)
- **Methods**: PascalCase (e.g., `GetWeatherAsync`, `OnInitializedAsync`)
- **Properties**: PascalCase (e.g., `TemperatureC`, `TemperatureF`)
- **Fields**: camelCase with underscore prefix for private fields (rarely used due to properties)
- **Parameters**: camelCase (e.g., `maxItems`, `cancellationToken`)
- **Local Variables**: camelCase (e.g., `forecasts`, `response`)
- **Constants**: PascalCase (e.g., `DefaultTimeout`)

#### File Organization
- One class/record per file (except for nested types)
- File names match the primary type name
- Place partial classes in separate files with descriptive suffixes

### Language Features

#### Modern C# Features
- **Implicit Usings**: Enabled - no need to manually add common using directives
- **Nullable Reference Types**: Enabled - use `?` for nullable references
- **Records**: Preferred for data models (e.g., `WeatherForecast`)
- **Primary Constructors**: Use for dependency injection (e.g., `WeatherApiClient(HttpClient httpClient)`)
- **Target-typed new**: Use when type is obvious from context
- **Collection Expressions**: Use `[]` for array initialization
- **Async/Await**: Always use async methods for I/O operations

#### Examples
```csharp
// Records for data models
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Primary constructors for services
public class WeatherApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}

// Collection expressions
string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];
```

### ASP.NET Core Patterns

#### API Controllers/Services
- Use minimal APIs with `MapGet`, `MapPost`, etc.
- Include OpenAPI documentation with `.WithName()` and `.WithDescription()`
- Handle errors with `UseExceptionHandler()` and `AddProblemDetails()`
- Use typed results for better API contracts

#### Dependency Injection
- Register services in `Program.cs` using `builder.Services.Add*`
- Use constructor injection for required dependencies
- Use `IHttpClientFactory` for HTTP clients with resilience policies
- Configure clients with service discovery schemes

#### Configuration
- Use `appsettings.json` for configuration
- Environment-specific overrides with `appsettings.Development.json`
- Typed configuration with `IOptions<T>`

### Blazor Components

#### Component Structure
- Use `@page` directive for routable components
- Place `@code` block at the end of the file
- Use `@inject` for dependency injection
- Prefer async lifecycle methods (`OnInitializedAsync`)

#### Razor Syntax
- Use PascalCase for component names and properties
- Use `@` for C# expressions in markup
- Use `@@` to escape `@` when needed
- Prefer conditional rendering with `@if`/`@else`

#### State Management
- Use component parameters for parent-to-child communication
- Use `EventCallback<T>` for child-to-parent communication
- Implement `INotifyPropertyChanged` or use `EventCallback` for state changes
- Use `StateHasChanged()` to trigger re-rendering when needed

### Error Handling

#### Exception Handling
- Use global exception handling with `UseExceptionHandler()`
- Return appropriate HTTP status codes (400 for bad requests, 500 for server errors)
- Use `ProblemDetails` for structured error responses
- Log exceptions with appropriate log levels

#### Validation
- Use data annotations for model validation
- Validate input parameters in API endpoints
- Return validation errors as `BadRequest` responses

### Logging and Observability

#### Logging
- Use structured logging with semantic values
- Include relevant context in log messages
- Use appropriate log levels (Debug, Information, Warning, Error)
- Configure logging in `Program.cs` with OpenTelemetry integration

#### Health Checks
- Implement health checks for critical dependencies
- Use `/health` endpoint for readiness probes
- Use `/alive` endpoint for liveness probes
- Tag health checks appropriately

### Testing

#### Test Structure
- Use NUnit framework with `[Test]` attribute
- Follow Arrange-Act-Assert pattern
- Use descriptive test method names
- Test one behavior per test method

#### Integration Testing
- Use Aspire testing framework for full application testing
- Test HTTP endpoints with `HttpClient`
- Wait for services to be healthy before testing
- Use appropriate timeouts for async operations

#### Test Naming
- Method names should describe the behavior being tested
- Use PascalCase for test method names
- Include the method under test and expected outcome

### Security Best Practices

#### Input Validation
- Always validate user input
- Use parameterized queries for database operations
- Sanitize data before processing

#### Authentication/Authorization
- Implement authentication as needed
- Use appropriate authorization policies
- Validate user permissions before allowing actions

#### HTTPS and Security Headers
- Use HTTPS in production
- Configure appropriate security headers
- Use HSTS for enhanced security

### Performance Considerations

#### Asynchronous Programming
- Use async/await for all I/O operations
- Avoid blocking calls in async methods
- Use `ConfigureAwait(false)` for library code

#### Caching
- Use output caching for expensive operations
- Cache static or infrequently changing data
- Configure appropriate cache durations

#### Resource Management
- Dispose of unmanaged resources properly
- Use `using` statements for disposable objects
- Implement proper cleanup in component disposal

### Code Organization

#### Imports
- Rely on implicit usings (enabled by default)
- Group related using statements
- Remove unused using directives

#### Comments
- Use XML documentation comments for public APIs
- Use `//` for implementation comments when needed
- Keep comments concise and meaningful
- Prefer self-documenting code over comments

#### Constants and Magic Numbers
- Extract magic numbers to named constants
- Use descriptive constant names
- Group related constants in static classes

## Development Workflow

1. **Setup**: Clone repository and run `dotnet restore`
2. **Development**: Use `dotnet run --project my-gpx-activities.AppHost` for local development
3. **Testing**: Run `dotnet test` frequently during development
4. **Code Review**: Ensure code follows these guidelines before submitting
5. **CI/CD**: Build and test commands should pass in automated pipelines

## Tooling

- **IDE**: Visual Studio 2022+ or VS Code with C# extensions
- **.NET Version**: .NET 10.0
- **Testing Framework**: NUnit 4.x
- **Web Framework**: ASP.NET Core with Blazor Server
- **API Documentation**: OpenAPI/Swagger (development only)
- **Observability**: OpenTelemetry with OTLP exporter support</content>
<parameter name="filePath">/home/frank/gh/my-gpx-activities/AGENTS.md