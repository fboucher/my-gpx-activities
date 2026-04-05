# Decision: Test Organization with Api/ Subdirectory

**Author:** Amos  
**Date:** 2026-04-05  
**Issue:** #40  
**Context:** Creating integration tests for Strava import endpoint

## Decision

Create an `Api/` subdirectory under `my-gpx-activities.Tests/` for API endpoint-specific integration tests.

**Structure:**
```
my-gpx-activities.Tests/
├── Api/
│   └── StravaImportTests.cs
├── HeatMapApiTests.cs
├── SmartMergeApiTests.cs
├── FitParserServiceTests.cs
└── WebTests.cs
```

## Rationale

1. **Scalability**: As more API endpoints are added, the test directory would become cluttered with many `*ApiTests.cs` files
2. **Organization**: Grouping related tests (API endpoint tests) together makes the project structure clearer
3. **Consistency**: Common .NET convention to organize tests by type/layer
4. **Future-proofing**: Leaves room for other subdirectories like `Services/`, `Parsers/`, etc.

## Migration Path

Existing API tests (`HeatMapApiTests.cs`, `SmartMergeApiTests.cs`) can optionally be moved to `Api/` in a future refactoring. Not required for consistency—having some tests in root and others in `Api/` is acceptable during transition.

## Alternative Considered

**Keep all tests in root directory**: Simpler initially but harder to navigate as project grows. With 5+ API endpoints, finding specific test files becomes harder.

## Notes

- Namespace remains `my_gpx_activities.Tests.Api` to match directory structure
- Build system handles subdirectories automatically (no csproj changes needed beyond existing patterns)
- This decision applies only to new tests; no mandate to migrate existing tests
