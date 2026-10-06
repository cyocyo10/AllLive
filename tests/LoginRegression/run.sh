#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.cache/cli-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.cache/nuget-packages}"
mkdir -p results
restore_args=(--configfile NuGet.Config)
if [[ -n "${LOGIN_NUGET_SOURCE:-}" ]]; then restore_args=(--source "$LOGIN_NUGET_SOURCE"); fi
"$DOTNET_BIN" restore LoginHarness.csproj "${restore_args[@]}" -p:NuGetAudit=false
"$DOTNET_BIN" build LoginHarness.csproj --no-restore --configuration Release
"$DOTNET_BIN" run --project LoginHarness.csproj --no-restore --no-build --configuration Release -- "$PWD/results/login.json"
