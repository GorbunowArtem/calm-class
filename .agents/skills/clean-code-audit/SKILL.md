---
name: clean-code-audit
description: Audit, verify, and enforce clean architecture, one-type-per-file, .editorconfig compliance, flattened decision trees, and no magic strings across C# code.
---

# Clean Code & Architecture Audit Skill

Use this skill whenever authoring, refactoring, or reviewing C# code in CalmClass to guarantee strict adherence to workspace standards.

## Mandatory Invariants

1. **One Type Per File**:
   - Every class, record, interface, enum, or struct MUST reside in its own dedicated `.cs` file.
   - Filename must match the type name exactly.

2. **No Magic Strings & Source-Generated Regex**:
   - Always use `[GeneratedRegex(...)]` with named capture groups (`(?<name>...)`).
   - Define constants or static configuration classes for commands, URLs, and templates.
   - Avoid redundant string interpolations.

3. **Linear Orchestration & Flattened Decision Trees**:
   - Keep `HandleAsync` and command handlers linear and clean.
   - Use early guard clauses and private helper methods instead of deeply nested if/else blocks.

4. **Separation of Concerns: Parsing & Validation Decomposition**:
   - Extract raw text tokenization, argument parsing, and boundary validations into dedicated services with interfaces (e.g. `ICreatePollArgsParser`).
   - Return strongly typed result records.
   - Register services in dependency injection (`ApplicationServiceExtensions.cs`).

5. **Strict `.editorconfig` Compliance**:
   - No multiple consecutive blank lines.
   - Using directives placed inside file-scoped namespace, sorted with `System` / `System.*` first then alphabetical.
   - Explicit braces on control statements, target-typed `new()`, and `var` usage.

## Automated Verification Steps

1. Run the static architecture auditor:
   ```bash
   python3 .agents/skills/clean-code-audit/scripts/audit.py
   ```

2. Build with zero warnings:
   ```bash
   export DOTNET_CLI_HOME=.dotnet
   export NUGET_PACKAGES=~/.nuget/packages
   dotnet build -m:1 --no-restore
   ```

3. Run full test suite:
   ```bash
   dotnet exec tests/unit/CalmClass.ApplicationTests.Unit/bin/Debug/net10.0/CalmClass.ApplicationTests.Unit.dll
   dotnet exec tests/unit/CalmClass.InfrastructureTests.Unit/bin/Debug/net10.0/CalmClass.InfrastructureTests.Unit.dll
   ```
