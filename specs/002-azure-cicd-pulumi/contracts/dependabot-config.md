# Configuration Contract: GitHub Dependabot

**Feature**: `specs/002-azure-cicd-pulumi`  
**Date**: 2026-09-06  
**Status**: Completed  

---

## 1. Specification Contract: `.github/dependabot.yml`

This document specifies the exact YAML configuration contract for GitHub Dependabot to automate dependency updates across application libraries, Pulumi C# packages, and GitHub Actions workflow actions.

```yaml
version: 2
updates:
  # Maintain NuGet dependencies across application and Pulumi IaC
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
      time: "06:00"
      timezone: "Europe/Kyiv"
    open-pull-requests-limit: 10
    commit-message:
      prefix: "chore(deps)"
      include: "scope"
    labels:
      - "dependencies"
      - "nuget"
    groups:
      pulumi-dependencies:
        applies-to: version-updates
        patterns:
          - "Pulumi*"
      framework-dependencies:
        applies-to: version-updates
        patterns:
          - "Microsoft.Azure.Functions*"
          - "Microsoft.Extensions*"
          - "Microsoft.Azure.Cosmos*"
      resilience-logging:
        applies-to: version-updates
        patterns:
          - "Polly*"
          - "Serilog*"

  # Maintain GitHub Actions dependencies
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "monday"
      time: "06:00"
      timezone: "Europe/Kyiv"
    open-pull-requests-limit: 5
    commit-message:
      prefix: "chore(ci)"
      include: "scope"
    labels:
      - "dependencies"
      - "github-actions"
```

---

## 2. Rules & Behaviors

1. **Central Package Management Synchronization**:
   Because `Directory.Packages.props` manages package versions centrally, Dependabot updates `<PackageVersion Include="..." Version="..." />` tags within `Directory.Packages.props`, ensuring atomic updates across the solution.

2. **Grouped Pull Requests**:
   Pulumi updates (`Pulumi`, `Pulumi.AzureNative`) are grouped together into a single PR (`pulumi-dependencies`) to prevent version drift between the core Pulumi engine and the Azure Native provider.

3. **Validation on Update PRs**:
   Every Dependabot PR automatically triggers the `.github/workflows/pr-ci-cd.yml` workflow, executing the Clean Code audit, solution compilation, unit tests, and the Pulumi preview to guarantee compatibility before merging.
