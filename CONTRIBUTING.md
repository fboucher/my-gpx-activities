# Contributing to my-gpx-activities

Thanks for your interest in contributing! We welcome bug reports, feature requests, and pull requests.

## Getting Started

1. **Fork** the repository on GitHub
2. **Clone** your fork locally
3. **Create a feature branch** from `dev`:
   ```bash
   git checkout -b feature/your-feature-name
   # or for squad work:
   git checkout -b squad/your-work-name
   ```

## Branch Naming

- **Feature work:** `feature/description` (e.g., `feature/add-cadence-chart`)
- **Squad/team work:** `squad/description` (e.g., `squad/readme-oss`)
- **Bug fixes:** `fix/description` (e.g., `fix/elevation-parsing`)

## Making Changes

1. Follow the code style and guidelines in [AGENTS.md](AGENTS.md)
2. Run tests locally before pushing:
   ```bash
   dotnet test
   ```
3. Build the project to catch any errors:
   ```bash
   dotnet build
   ```

## Submitting a Pull Request

1. Push your branch to your fork
2. Open a pull request against the `dev` branch (not `main`)
3. Provide a clear title and description:
   - What problem does this solve?
   - What changes did you make?
   - How can reviewers test this?
4. Respond to any feedback from reviewers

## Code Style

See [AGENTS.md](AGENTS.md) for:
- C# naming conventions (PascalCase, camelCase)
- Async/await patterns
- Error handling
- Testing guidelines
- Blazor component structure

Key points:
- Use modern C# features (records, primary constructors, nullable reference types)
- Write async methods for I/O operations
- Keep components and classes focused on a single responsibility
- Test your changes with NUnit

## Questions?

Open an issue on GitHub or reach out via GitHub Discussions. We're here to help!

Thanks for contributing to my-gpx-activities. 🎉
