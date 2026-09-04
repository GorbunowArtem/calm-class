# Workspace Agent Instructions

## C# Coding Standards & Conventions

- **Primary Constructors (Mandatory):**
  - Always use C# primary constructors for all classes requiring dependency injection, parameters, or initialization.
  - Do NOT declare redundant private backing fields (e.g. `private readonly IFoo _foo;`).
  - Do NOT prefix constructor parameters or fields with an underscore (omit `_*`).
  - Reference the primary constructor parameters directly across instance methods using standard camelCase naming (e.g. `foo.DoSomething()`).
  - When secondary constructors are needed, chain to the primary constructor using `: this(...)`.

- **Immutability & Records:**
  - Leverage C# `record` types (`record class` or `record struct`) for domain entities, DTOs, data contracts, and document models.

## Spec-Driven Development & Living Documentation

- **Living Spec Synchronization:**
  - Specifications (`spec.md`) are living source-of-truth documents.
  - Upon completing any feature implementation or significant code change, review and synchronize `spec.md` (and related `contracts/`) with the as-built reality (including edge cases, parameter adjustments, or schema nuances).
