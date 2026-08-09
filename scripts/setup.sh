#!/bin/zsh
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_root"
dotnet restore Alchemy.slnx
dotnet build Alchemy.slnx --configuration Debug --no-restore
