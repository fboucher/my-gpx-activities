# Decision: Always Materialize LINQ Collections Before Returning from API Endpoints

**Context:** Issue #57 revealed that chained deferred LINQ execution (.Select() on .Select()) can cause silent response truncation when exceptions occur during JSON serialization.

**Problem:**
When API endpoints return `IEnumerable<T>` with deferred LINQ operations, ASP.NET Core's JSON serializer enumerates the collection during response writing. If an exception occurs during enumeration (null reference, data corruption, serialization error), it can cause silent truncation mid-stream rather than proper error handling.

**Example of problematic pattern:**
```csharp
// Repository
return activities.Select(MapToActivity);  // Deferred

// Endpoint
var response = activities.Select(a => new { ... });  // Deferred on deferred
return Results.Ok(response);  // Serializer enumerates here
```

If MapToActivity throws or serialization fails partway through, you get partial results (e.g., 11 out of 20 items).

**Decision:**
Always materialize collections with `.ToList()` or `.ToArray()` before returning from API endpoints and repositories.

**Correct pattern:**
```csharp
// Repository
return activities.Select(MapToActivity).ToList();  // Materialized

// Endpoint
var response = activities.Select(a => new { ... }).ToList();  // Materialized
return Results.Ok(response);
```

**Benefits:**
1. Exceptions occur within the async method's scope and are caught by exception handler middleware
2. Proper 500 errors instead of silent truncation
3. Better debuggability — full stack trace instead of mid-serialization failure
4. More predictable behavior

**Applies to:**
- All GET endpoints returning collections
- Repository methods returning `IEnumerable<T>`
- Any LINQ projection that will be JSON-serialized

**Related:**
- Issue #57 (activities list truncation)
- PR #59 (fix implementation)

**Author:** Naomi (Backend Dev)
**Date:** 2026-04-XX
