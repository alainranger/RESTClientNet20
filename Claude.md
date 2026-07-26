# CLAUDE.md - RESTClient Library for .NET Framework 2.0


## Overview
The RESTClient library is a lightweight and easy-to-use HTTP client for .NET Framework 2.0. 
It provides a simple interface for making HTTP requests and handling responses, making it ideal for developers who need to interact with RESTful APIs in their applications.

## Tech Stack
- .NET Framework 2.0
- Newtonsoft.Json for JSON serialization and deserialization

## Project Structure
- `src/` - Source code for the library
- `tests/` - Unit and integration tests
- `docs/` - Documentation and guides

## Commands
- `build` - Compiles the source code and generates the library DLL.
- `test` - Runs the unit and integration tests to ensure the library functions correctly.
- `package` - Creates a NuGet package for distribution.
- `publish` - Publishes the NuGet package to a specified feed or repository.
- `clean` - Cleans up build artifacts and temporary files.
- `restore` - Restores any dependencies required for the project.

## Testing
- Integration tests: Full API endpoint testing with WebApplicationFactory
- Use FluentAssertions for readable assertions
- Test naming: `[Method]_[Scenario]_[ExpectedResult]`

## Git Workflow
- Branch naming: `feature/`, `bugfix/`, `hotfix/`
- Commit format: `type: description` (feat, fix, refactor, test, docs)
- Always create a branch before changes
- Run tests before committing