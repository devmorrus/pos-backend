#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

[[ "${1:-}" == "--confirm-new-repository" ]] \
  || fail "Usage: $0 --confirm-new-repository"

require_backup_config
prepare_runtime_directories
acquire_operation_lock

if restic_run snapshots >/dev/null 2>&1; then
  echo "Restic repository already initialized and readable."
  exit 0
fi

restic_run init
restic_run check
echo "Restic repository initialized and verified."
