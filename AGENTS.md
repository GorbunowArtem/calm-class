# Workspace Agent Instructions

## C# Coding Standards & Conventions

- **Primary Constructors (Mandatory):**
  - Always use C# primary constructors for all classes requiring dependency injection, parameters, or initialization.
  - Do NOT declare redundant private backing fields (e.g. `private readonly IFoo _foo;`).
  - Do NOT prefix constructor parameters or fields with an underscore (omit `_*`).
  - Reference the primary constructor parameters directly across instance methods using standard camelCase naming (e.g. `foo.DoSomething()`).
  - When secondary constructors are needed, chain to the primary constructor using `: this(...)`.

- **Immutability & Positional Records (Mandatory):**
  - Leverage concise C# positional `record` types (`record class` or `record struct`) for domain entities, DTOs, data contracts, and document models.
  - Serialization and validation attributes MUST explicitly use the `[property: ...]` target prefix (e.g. `[property: JsonPropertyName("...")]`, `[property: Required]`) so attributes attach directly to the compiler-generated public property.
  - Use `[field: ...]` when targeting compiler-generated private backing fields (e.g. `[field: NonSerialized]`).
  - Avoid verbose nominal record property declarations (`{ get; init; }`) when positional syntax can express the contract concisely.

- **Using Directive Placement & Sorting (IDE0065 - Mandatory):**
  - In accordance with `.editorconfig` (`csharp_using_directive_placement = inside_namespace:error`), all `using` directives in C# files MUST be placed inside/below the file-scoped `namespace <Name>;` declaration.
  - Organize and sort using directives with `System` / `System.*` directives first, followed by other directives alphabetically (`dotnet_sort_system_directives_first = true`).
  - Top-level statement entry points (e.g., `Program.cs`) lacking a namespace declaration are the sole exception.

## Spec-Driven Development & Living Documentation

- **Living Spec Synchronization:**
  - Specifications (`spec.md`) are living source-of-truth documents.
  - Upon completing any feature implementation or significant code change, review and synchronize `spec.md` (and related `contracts/`) with the as-built reality (including edge cases, parameter adjustments, or schema nuances).
