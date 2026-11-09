#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

MAX_AGE_HOURS="${1:-${BACKUP_MAX_AGE_HOURS:-8}}"
[[ "${MAX_AGE_HOURS}" =~ ^[0-9]+$ ]] || fail "Maximum backup age harus berupa jam bulat"

require_backup_config
prepare_runtime_directories

SNAPSHOTS_JSON="$(mktemp "${BACKUP_STAGING_DIR}/snapshots.XXXXXX")"
trap 'rm -f "${SNAPSHOTS_JSON}"' EXIT
restic_run snapshots \
  --host "${BACKUP_HOST_ID}" \
  --tag application:morrus-pos \
  --latest 1 \
  --json > "${SNAPSHOTS_JSON}"

SNAPSHOT_TIME="$(sed -n 's/.*"time":"\([^"]*\)".*/\1/p' "${SNAPSHOTS_JSON}" | head -n 1)"
SNAPSHOT_ID="$(sed -n 's/.*"short_id":"\([^"]*\)".*/\1/p' "${SNAPSHOTS_JSON}" | head -n 1)"
[[ -n "${SNAPSHOT_TIME}" ]] || fail "Tidak ada snapshot MorrusPOS yang dapat dipakai"

SNAPSHOT_EPOCH="$(date -d "${SNAPSHOT_TIME}" +%s)"
NOW_EPOCH="$(date +%s)"
AGE_SECONDS=$((NOW_EPOCH - SNAPSHOT_EPOCH))
MAX_AGE_SECONDS=$((MAX_AGE_HOURS * 3600))
(( AGE_SECONDS >= 0 )) || fail "Timestamp snapshot berada di masa depan: ${SNAPSHOT_TIME}"
(( AGE_SECONDS <= MAX_AGE_SECONDS )) \
  || fail "Backup terakhir terlalu lama: snapshot=${SNAPSHOT_ID:-unknown}, age_seconds=${AGE_SECONDS}, max_seconds=${MAX_AGE_SECONDS}"

echo "Backup is fresh. Snapshot=${SNAPSHOT_ID:-unknown} AgeSeconds=${AGE_SECONDS}"
