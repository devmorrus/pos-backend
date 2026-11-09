#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

SNAPSHOT=""
DATABASE_VOLUME=""
UPLOADS_VOLUME=""
CONFIRMED=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --snapshot) SNAPSHOT="${2:-}"; shift 2 ;;
    --database-volume) DATABASE_VOLUME="${2:-}"; shift 2 ;;
    --uploads-volume) UPLOADS_VOLUME="${2:-}"; shift 2 ;;
    --confirm-create-only) CONFIRMED=true; shift ;;
    *) fail "Unknown argument: $1" ;;
  esac
done

[[ "${CONFIRMED}" == "true" && -n "${SNAPSHOT}" && -n "${DATABASE_VOLUME}" && -n "${UPLOADS_VOLUME}" ]] \
  || fail "Usage: $0 --snapshot ID --database-volume NAME --uploads-volume NAME --confirm-create-only"
[[ "${SNAPSHOT}" =~ ^[a-fA-F0-9]{64}$ ]] || fail "Recovery permanen wajib memakai full 64-character snapshot ID"
[[ "${DATABASE_VOLUME}" == morrus-pos_pgdata_recovery_* ]] \
  || fail "Database volume wajib memakai prefix morrus-pos_pgdata_recovery_"
[[ "${UPLOADS_VOLUME}" == morrus-pos_uploads_recovery_* ]] \
  || fail "Uploads volume wajib memakai prefix morrus-pos_uploads_recovery_"
[[ "${DATABASE_VOLUME}" != "${POSTGRES_DATA_VOLUME_NAME}" ]] || fail "Target sama dengan database production"
[[ "${UPLOADS_VOLUME}" != "${UPLOADS_VOLUME_NAME}" ]] || fail "Target sama dengan uploads production"
[[ -n "${MORRUS_APP_PASSWORD:-}" ]] || fail "MORRUS_APP_PASSWORD wajib diexport untuk recovery permanen"
[[ -n "${MORRUS_SYSTEM_PASSWORD:-}" ]] || fail "MORRUS_SYSTEM_PASSWORD wajib diexport untuk recovery permanen"
[[ -n "${MORRUS_MIGRATION_PASSWORD:-}" ]] || fail "MORRUS_MIGRATION_PASSWORD wajib diexport untuk recovery permanen"

export RECOVERY_DATABASE_VOLUME="${DATABASE_VOLUME}"
export RECOVERY_UPLOADS_VOLUME="${UPLOADS_VOLUME}"
export RECOVERY_ADMIN_PASSWORD="${MORRUS_MIGRATION_PASSWORD}"
export RECOVERY_APP_PASSWORD="${MORRUS_APP_PASSWORD}"
export RECOVERY_SYSTEM_PASSWORD="${MORRUS_SYSTEM_PASSWORD}"
export RECOVERY_MIGRATION_PASSWORD="${MORRUS_MIGRATION_PASSWORD}"
export KEEP_RECOVERY_VOLUMES=true
exec "${SCRIPT_DIR}/restore-drill.sh" "${SNAPSHOT}"
