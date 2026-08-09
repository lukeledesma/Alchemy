#!/bin/zsh
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
zsh "$project_root/scripts/build.sh" Debug
