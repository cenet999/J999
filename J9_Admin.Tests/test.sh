#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet test J9_Admin.Tests.csproj "$@"
