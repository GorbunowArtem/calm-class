#!/usr/bin/env bash
# ==============================================================================
# CalmClass - Launch Functions Locally
# Usage: ./scripts/run-local.sh
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
FUNCTIONS_DIR="$ROOT_DIR/src/CalmClass.Functions"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT_DIR/.dotnet}"
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=true
export NUGET_PACKAGES="${NUGET_PACKAGES:-/Users/Artem_Horbunov1/.nuget/packages}"

echo "=== CalmClass Local Environment Check ==="

# 1. Check .NET SDK
if ! command -v dotnet >/dev/null 2>&1; then
  echo "❌ .NET SDK is not installed. Please install .NET 10."
  exit 1
fi
echo "✓ .NET SDK: $(dotnet --version)"

# 2. Check Azure Functions Core Tools
if ! command -v func >/dev/null 2>&1; then
  echo "⚠️  Azure Functions Core Tools ('func') is not found on your PATH."
  echo "To install on macOS:"
  echo "  brew tap azure/azure-functions"
  echo "  brew install azure-functions-core-tools@4"
  echo "Or via npm:"
  echo "  npm install -g azure-functions-core-tools@4 --unsafe-perm true"
  echo ""
  read -r -p "Do you want to attempt building the project instead? (y/n): " CONTINUE
  if [[ "$CONTINUE" =~ ^[Yy]$ ]]; then
    DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=true dotnet build -m:1 "$ROOT_DIR/CalmClass.slnx"
    echo "✅ Build completed successfully."
  fi
  exit 0
fi
echo "✓ Azure Functions Core Tools: $(func --version)"

# 3. Ensure local.settings.json exists
if [[ ! -f "$FUNCTIONS_DIR/local.settings.json" ]]; then
  echo "Creating local.settings.json from local.settings.example.json..."
  cp "$FUNCTIONS_DIR/local.settings.example.json" "$FUNCTIONS_DIR/local.settings.json"
  echo "✓ Created $FUNCTIONS_DIR/local.settings.json"
fi

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT_DIR/.dotnet}"
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=true

# 4. Build and start Azure Functions host
echo ""
echo "Building CalmClass.Functions..."
dotnet build -m:1 "$FUNCTIONS_DIR/CalmClass.Functions.csproj"

echo ""
echo "Starting CalmClass Functions locally on http://localhost:7071..."
cd "$FUNCTIONS_DIR/bin/Debug/net10.0"
exec func start
