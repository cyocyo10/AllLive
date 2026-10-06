#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/.cache/cli-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$PWD/.cache/nuget-packages}"
mkdir -p results
for pair in main:AuditHarness douyin:DouyinHarness sync:SyncHarness tls-sites:SiteTlsHarness warnings:WarningHarness; do
  folder="${pair%%:*}"; name="${pair##*:}"; project="$folder/$name.csproj"
  "$DOTNET_BIN" restore "$project" --configfile NuGet.Config
  "$DOTNET_BIN" build "$project" --no-restore --configuration Release
  "$DOTNET_BIN" run --project "$project" --no-restore --no-build --configuration Release -- "$PWD/results/$folder.json"
done
