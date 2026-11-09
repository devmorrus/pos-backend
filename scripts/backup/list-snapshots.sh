#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

require_backup_config
prepare_runtime_directories
restic_run snapshots --host "${BACKUP_HOST_ID}" --tag application:morrus-pos --json
