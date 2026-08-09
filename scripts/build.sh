#!/bin/zsh
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: scripts/build.sh <Debug|Release>" >&2
  exit 2
fi

configuration="$1"
if [[ "$configuration" != "Debug" && "$configuration" != "Release" ]]; then
  echo "Invalid configuration: $configuration" >&2
  echo "Expected Debug or Release" >&2
  exit 2
fi

project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"

dotnet restore Alchemy.slnx
dotnet build Alchemy.slnx --configuration "$configuration" --no-restore
