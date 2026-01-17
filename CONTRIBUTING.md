# Contributing to MilMap

Thank you for your interest in contributing to MilMap! This document provides guidelines for contributing to the project.

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- Git

### Setting Up the Development Environment

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/your-username/MilMap.git
   cd MilMap
   ```
3. Build the project:
   ```bash
   dotnet build
   ```
4. Run tests to ensure everything works:
   ```bash
   dotnet test
   ```

## Development Workflow

### Branching Strategy

- `main` - Stable release branch
- Feature branches - Create from `main` for new features or bug fixes

### Making Changes

1. Create a new branch for your work:
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. Make your changes following the coding standards below

3. Write or update tests as needed

4. Ensure all tests pass:
   ```bash
   dotnet test
   ```

5. Commit your changes with a descriptive message:
   ```bash
   git commit -m "Add feature: description of what you added"
   ```

6. Push to your fork and create a pull request

## Coding Standards

### C# Style Guidelines

- Use C# 12 features where appropriate
- Follow [Microsoft's C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use file-scoped namespaces
- Prefer `var` when the type is obvious
- Use nullable reference types (`#nullable enable`)
- Add XML documentation comments for public APIs

### Project Organization

- **MilMap.Core** - Core library with no dependencies on CLI concerns
- **MilMap.CLI** - Command-line interface, depends on Core
- **MilMap.Tests** - Unit tests for both projects

### Naming Conventions

- Classes, methods, properties: `PascalCase`
- Private fields: `_camelCase`
- Local variables, parameters: `camelCase`
- Constants: `PascalCase`
- Interfaces: `IPascalCase`

### Example

```csharp
namespace MilMap.Core.Rendering;

/// <summary>
/// Renders contour lines from elevation data.
/// </summary>
public class ContourRenderer
{
    private readonly ElevationSource _elevationSource;

    public ContourRenderer(ElevationSource elevationSource)
    {
        _elevationSource = elevationSource ?? throw new ArgumentNullException(nameof(elevationSource));
    }

    /// <summary>
    /// Renders contour lines for the specified bounding box.
    /// </summary>
    /// <param name="bounds">The geographic bounds to render.</param>
    /// <param name="intervalMeters">Contour interval in meters.</param>
    /// <returns>A collection of contour line geometries.</returns>
    public IEnumerable<ContourLine> Render(BoundingBox bounds, int intervalMeters = 20)
    {
        // Implementation
    }
}
```

## Testing

### Writing Tests

- Place tests in `tests/MilMap.Tests/`
- Mirror the source project structure
- Use descriptive test method names: `MethodName_Scenario_ExpectedResult`
- Use xUnit for testing

### Test Example

```csharp
public class MgrsParserTests
{
    [Fact]
    public void Parse_ValidGridSquare_ReturnsCorrectCoordinates()
    {
        // Arrange
        var input = "18TXM";

        // Act
        var result = MgrsParser.Parse(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("18T", result.GridZone);
        Assert.Equal("XM", result.SquareId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("99ZZZ")]
    public void Parse_InvalidInput_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => MgrsParser.Parse(input));
    }
}
```

## Pull Request Process

1. Ensure your code follows the coding standards
2. Update documentation if you're changing functionality
3. Add or update tests as appropriate
4. Ensure all tests pass
5. Update the README if adding new features
6. Create a pull request with a clear description of changes

### Pull Request Title Format

- `feat: Add legend generation`
- `fix: Correct MGRS parsing for southern hemisphere`
- `docs: Update installation instructions`
- `refactor: Simplify tile caching logic`
- `test: Add tests for elevation source`

## Reporting Issues

When reporting issues, please include:

1. A clear, descriptive title
2. Steps to reproduce the issue
3. Expected behavior
4. Actual behavior
5. Your environment (OS, .NET version)
6. Any relevant logs or error messages

## Feature Requests

Feature requests are welcome! Please:

1. Check existing issues to avoid duplicates
2. Describe the use case for the feature
3. Explain how it would benefit users
4. Be open to discussion about implementation

## Questions?

If you have questions about contributing, feel free to open an issue for discussion.
