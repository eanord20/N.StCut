# N.StCut — Project Rules

## Project Goal

N.StCut is a Windows desktop application for automatic sheet nesting of DXF flat patterns.

Priority:

1. Correct geometry
2. Deterministic nesting
3. Stable production behavior
4. Performance

---

## Technology

- Language: C#
- Platform: .NET 8
- UI: WPF
- DXF library: project-defined
- Target OS: Windows 10/11

---

## Geometry Rules

- Geometry is the source of truth.
- Never modify original DXF entities.
- Import DXF as immutable source data.
- All transformations use copied geometry.
- Preserve units exactly.
- Preserve rotation precision.

---

## Nesting Rules

- Results must be deterministic.
- Identical input always produces identical layouts.
- Grain direction is mandatory when enabled.
- Part quantity is never altered by the algorithm.
- Sheet boundaries are absolute constraints.

---

## Architecture

Use this structure:

/Core
    Geometry
    Nesting
    Optimization

/Infrastructure
    Dxf
    Export
    Persistence

/UI
    Views
    ViewModels

Business logic must never depend on WPF.

---

## Before Writing Code

Always search for:

- existing geometry utilities
- rectangle/intersection algorithms
- DXF parsers
- optimization helpers

Reuse before creating new code.

---

## Completion Checklist

- No hardcoded values
- No duplicated logic
- COM not applicable unless explicitly added
- Project builds without warnings
- Existing behavior preserved

---

## Build & Test Commands

- Build: `dotnet build src/NStCut.sln`
- App tests: `dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj`
- Nesting tests: `dotnet test src/NStCut.Nesting.Tests/NStCut.Nesting.Tests.csproj`
- Reports tests: `dotnet test src/NStCut.Reports.Tests/NStCut.Reports.Tests.csproj`
- Integration tests: `dotnet test src/NStCut.IntegrationTests/NStCut.IntegrationTests.csproj`
- DXF tests: `dotnet test src/NStCut.Dxf.Tests/NStCut.Dxf.Tests.csproj`
- Core tests: `dotnet test src/NStCut.Core.Tests/NStCut.Core.Tests.csproj`