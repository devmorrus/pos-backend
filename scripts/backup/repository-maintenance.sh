#!/usr/bin/env bash

set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

require_backup_config
prepare_runtime_directories
acquire_operation_lock

METRICS_FILE="${BACKUP_METRICS_DIR}/repository.prom"
START_EPOCH="$(date +%s)"
LAST_SUCCESS="$(read_metric_value "${METRICS_FILE}" morrus_backup_repository_check_last_success_timestamp_seconds || true)"
LAST_SUCCESS="${LAST_SUCCESS:-0}"
RESULT=0

write_metrics() {
  local now duration temporary
  now="$(date +%s)"
  duration=$((now - START_EPOCH))
  temporary="${METRICS_FILE}.tmp.$$"
  cat > "${temporary}" <<EOF
# HELP morrus_backup_repository_check_last_attempt_timestamp_seconds Unix time of the last repository maintenance attempt.
# TYPE morrus_backup_repository_check_last_attempt_timestamp_seconds gauge
morrus_backup_repository_check_last_attempt_timestamp_seconds ${START_EPOCH}
# HELP morrus_backup_repository_check_last_success_timestamp_seconds Unix time of the last successful repository check.
# TYPE morrus_backup_repository_check_last_success_timestamp_seconds gauge
morrus_backup_repository_check_last_success_timestamp_seconds ${LAST_SUCCESS}
# HELP morrus_backup_repository_check_last_duration_seconds Duration of the last repository maintenance attempt.
# TYPE morrus_backup_repository_check_last_duration_seconds gauge
morrus_backup_repository_check_last_duration_seconds ${duration}
# HELP morrus_backup_repository_check_last_result Whether the last repository maintenance succeeded (1) or failed (0).
# TYPE morrus_backup_repository_check_last_result gauge
morrus_backup_repository_check_last_result ${RESULT}
EOF
  mv "${temporary}" "${METRICS_FILE}"
  chmod 644 "${METRICS_FILE}"
}

finish() {
  local exit_code=$?
  trap - EXIT
  write_metrics
  exit "${exit_code}"
}
trap finish EXIT

restic_run forget \
  --host "${BACKUP_HOST_ID}" \
  --tag application:morrus-pos \
  --keep-within 7d \
  --keep-daily 35 \
  --keep-weekly 12 \
  --keep-monthly 12 \
  --prune

restic_run check --read-data-subset="${RESTIC_CHECK_SUBSET}"

RESULT=1
LAST_SUCCESS="$(date +%s)"
echo "Restic retention, prune, and repository check completed."
