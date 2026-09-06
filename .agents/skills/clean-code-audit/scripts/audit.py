#!/usr/bin/env python3
"""
Clean Code & Architecture Validator for CalmClass (.NET 10).
Checks:
1. One Type Per File: Every .cs file contains at most one class, record, interface, enum, or struct.
2. Filename matches type name.
3. Using directives placed inside file-scoped namespace (except top-level Program.cs).
4. Using directives sorted with System namespaces first, then segment-wise alphabetical (Roslyn rules).
5. No consecutive blank lines (.editorconfig violation).
6. No raw inline regex or positional group indexing (magic numbers).
"""

import os
import re
import sys

TYPE_PATTERN = re.compile(
    r"^\s*(public|internal|private|protected)?\s*(static|sealed|abstract|partial)?\s*(class|record|interface|enum|struct)\s+(\w+)",
    re.MULTILINE
)
POSITIONAL_GROUP_PATTERN = re.compile(r"\.Groups\[\d+\]")

def using_sort_key(using_line):
    # Strip "using " and trailing ";"
    ns = using_line.replace("using ", "").rstrip(";").strip()
    is_system = ns == "System" or ns.startswith("System.")
    segments = ns.split(".")
    # Group System first (0), then others (1). Within each group, sort by segments.
    return (0 if is_system else 1, segments)

def audit_csharp_files(root_dir="."):
    violations = []

    for root, dirs, files in os.walk(root_dir):
        if any(ignored in root for ignored in ["bin", "obj", ".git", ".vs"]):
            continue

        for file in files:
            if not file.endswith(".cs"):
                continue

            path = os.path.join(root, file)
            basename = os.path.splitext(file)[0]

            with open(path, "r", encoding="utf-8") as f:
                content = f.read()

            lines = content.split("\n")

            # 1 & 2: Single Type Per File & naming check
            matches = TYPE_PATTERN.findall(content)
            type_names = [m[3] for m in matches]

            if len(type_names) > 1:
                violations.append(f"{path}: Multiple types declared: {type_names} (violates One Type Per File)")
            elif len(type_names) == 1 and type_names[0] != basename:
                violations.append(f"{path}: Type '{type_names[0]}' does not match filename '{basename}'")

            # 3 & 4: Usings placement and sorting
            ns_index = -1
            usings = []
            for i, line in enumerate(lines):
                trimmed = line.strip()
                if line.rstrip() != line:
                    violations.append(f"{path}:{i+1} has trailing whitespace")

                if trimmed.startswith("namespace ") and trimmed.endswith(";"):
                    ns_index = i
                elif trimmed.startswith("using ") and trimmed.endswith(";") and not trimmed.startswith("using var "):
                    if ns_index == -1 and basename != "Program":
                        violations.append(f"{path}:{i+1} using directive declared before file-scoped namespace")
                    usings.append(trimmed)

            if basename != "Program" and usings:
                expected_usings = sorted(usings, key=using_sort_key)
                if usings != expected_usings:
                    violations.append(f"{path}: Using directives not sorted according to Roslyn standards (System first, then alphabetical segments). Found: {usings}, Expected: {expected_usings}")

            # 5: Consecutive blank lines
            for i in range(len(lines) - 1):
                if lines[i] == "" and lines[i+1] == "":
                    # Check if not at EOF
                    if any(l.strip() for l in lines[i+2:]):
                        violations.append(f"{path}:{i+1} Multiple consecutive blank lines detected")

            # 6: Magic regex positional indexing
            pos_matches = POSITIONAL_GROUP_PATTERN.findall(content)
            if pos_matches:
                violations.append(f"{path}: Positional regex group indexing {pos_matches} detected; use named groups")

    return violations

if __name__ == "__main__":
    target_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    errors = audit_csharp_files(target_dir)

    if errors:
        print(f"FAILED: Found {len(errors)} clean code violations:")
        for err in errors:
            print(f"  - {err}")
        sys.exit(1)
    else:
        print("SUCCESS: All C# files comply with Clean Code & Architecture standards!")
        sys.exit(0)
