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

- **Single Type Per File (Mandatory):**
  - Every class, record, interface, enum, or struct MUST reside in its own dedicated `.cs` file.
  - The filename MUST strictly match the type name (e.g., `TelegramPollResult.cs` for `TelegramPollResult`).
  - Do NOT place multiple types in a single file (e.g., commands, command results, DTOs, options, or domain entities must each have their own independent file).

- **No Magic Strings & Source-Generated Regex (Mandatory):**
  - Never use raw inline regex strings or positional regex group indexes (e.g. `match.Groups[1]`). Always declare C# source-generated regex via `[GeneratedRegex(...)]` with descriptive named capture groups (`(?<name>...)`).
  - Do not use hardcoded command strings, endpoints, or error message templates inline; define strongly typed constants or static configuration classes (e.g. `private const string CommandName = "/create_poll";`).
  - Never use redundant string interpolations (e.g. `$"{ConstantString}"`).

- **Linear Orchestration & Flattened Decision Trees (Mandatory):**
  - Prohibit deeply nested if/else logic, complex decision trees, and monolithic handler methods.
  - Structure orchestration methods (such as `HandleAsync`) into clean, linear pipelines with early guard clauses.
  - Extract validation checks, conflict detections, and side-effect dispatchers into descriptive private helper methods (e.g. `IsAuthorized...`, `HasConflict...`, `FailAsync`, `PublishAndTrack...`).

- **Separation of Concerns: Parsing & Validation Decomposition (Mandatory):**
  - Handlers MUST NOT embed raw parsing, tokenization, or boundary validation logic.
  - Extract parsing, tokenization, and constraint validation into dedicated, testable services with interfaces (e.g. `ICreatePollArgsParser` / `CreatePollArgsParser`).
  - Return strongly typed result records (e.g. `ArgsResolutionResult`) rather than throwing exceptions or returning untyped tuples for anticipated business validation flows.
  - Register all parsers and validators in dependency injection (`ApplicationServiceExtensions.cs`) and cover them with isolated unit tests.

- **Strict `.editorconfig` Compliance (Mandatory):**
  - Zero tolerance for multiple consecutive blank lines (`dotnet_style_allow_multiple_blank_lines_experimental = false:error`).
  - In accordance with `.editorconfig` (`csharp_using_directive_placement = inside_namespace:error`), all `using` directives in C# files MUST be placed inside/below the file-scoped `namespace <Name>;` declaration.
  - Organize and sort using directives with `System` / `System.*` directives first, followed by other directives alphabetically (`dotnet_sort_system_directives_first = true`).
  - Top-level statement entry points (e.g., `Program.cs`) lacking a namespace declaration are the sole exception.
  - Always use `var` for local declarations where required by `.editorconfig`.
  - Use target-typed `new()` expressions when the type is apparent.
  - Explicit braces `{}` are mandatory for all control-flow statements (`csharp_prefer_braces = true:warning`).
  - Maximum line length is 166 characters; wrap arguments and object initializers cleanly.

- **System.Text.Json Exclusively (Mandatory):**
  - Always use `System.Text.Json` and `System.Text.Json.Serialization` for all JSON operations, DTO serialization, and document mapping.
  - The `Newtonsoft.Json` library is NOT preferred and MUST NOT be referenced or used across any project.
  - Serialization and validation attributes on positional records MUST explicitly use `[property: JsonPropertyName("...")]`.
  - For Azure Cosmos DB, configure `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions`.

## Spec-Driven Development & Living Documentation

- **Living Spec Synchronization:**
  - Specifications (`spec.md`) are living source-of-truth documents.
  - Upon completing any feature implementation or significant code change, review and synchronize `spec.md` (and related `contracts/`) with the as-built reality (including edge cases, parameter adjustments, or schema nuances).
