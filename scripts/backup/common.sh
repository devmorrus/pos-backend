#!/usr/bin/env bash

set -euo pipefail

BACKUP_ENV_FILE="${BACKUP_ENV_FILE:-/etc/morrus-pos/backup.env}"
if [[ -f "${BACKUP_ENV_FILE}" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "${BACKUP_ENV_FILE}"
  set +a
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MORRUS_PROJECT_DIR="${MORRUS_PROJECT_DIR:-$(cd "${SCRIPT_DIR}/../.." && pwd)}"
RESTIC_IMAGE="${RESTIC_IMAGE:-restic/restic:0.18.1}"
POSTGRES_IMAGE="${POSTGRES_IMAGE:-postgres:15-alpine}"
ALPINE_IMAGE="${ALPINE_IMAGE:-alpine:3.20}"
POSTGRES_DATABASE="${POSTGRES_DATABASE:-morrusposs}"
POSTGRES_ADMIN_USER="${POSTGRES_ADMIN_USER:-morrus}"
POSTGRES_DATA_VOLUME_NAME="${POSTGRES_DATA_VOLUME_NAME:-morrus-pos_pgdata}"
UPLOADS_VOLUME_NAME="${UPLOADS_VOLUME_NAME:-morrus-pos_uploads}"
BACKUP_HOST_ID="${BACKUP_HOST_ID:-morrus-pos-production}"
BACKUP_STAGING_DIR="${BACKUP_STAGING_DIR:-/var/lib/morrus-pos/backup-staging}"
BACKUP_METRICS_DIR="${BACKUP_METRICS_DIR:-/var/lib/morrus-pos/backup-metrics}"
RESTIC_CACHE_DIR="${RESTIC_CACHE_DIR:-/var/cache/morrus-pos/restic}"
BACKUP_LOCK_FILE="${BACKUP_LOCK_FILE:-/var/lock/morrus-pos/backup.lock}"
BACKUP_SHARED_GROUP="${BACKUP_SHARED_GROUP:-morrusdigital}"
PG_DUMP_COMPRESSION="${PG_DUMP_COMPRESSION:-6}"
RESTIC_CHECK_SUBSET="${RESTIC_CHECK_SUBSET:-5%}"

COMPOSE=(docker compose -f "${MORRUS_PROJECT_DIR}/docker-compose.yml" -f "${MORRUS_PROJECT_DIR}/ops/observability/docker-compose.observability.yml")

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Command wajib tidak ditemukan: $1"
}

require_backup_config() {
  [[ -n "${RESTIC_REPOSITORY:-}" ]] || fail "RESTIC_REPOSITORY wajib diisi"
  [[ -n "${RESTIC_PASSWORD_FILE:-}" ]] || fail "RESTIC_PASSWORD_FILE wajib diisi"
  [[ -r "${RESTIC_PASSWORD_FILE}" ]] || fail "RESTIC_PASSWORD_FILE tidak dapat dibaca: ${RESTIC_PASSWORD_FILE}"
  require_command docker
}

prepare_runtime_directories() {
  mkdir -p "${BACKUP_STAGING_DIR}" "${BACKUP_METRICS_DIR}" "${RESTIC_CACHE_DIR}" "$(dirname "${BACKUP_LOCK_FILE}")"
  chmod 700 "${BACKUP_STAGING_DIR}" "${RESTIC_CACHE_DIR}" "$(dirname "${BACKUP_LOCK_FILE}")"
  chmod 755 "${BACKUP_METRICS_DIR}"
}

acquire_operation_lock() {
  require_command flock
  if [[ ! -e "${BACKUP_LOCK_FILE}" ]]; then
    (umask 0007; : > "${BACKUP_LOCK_FILE}")
  fi
  chgrp "${BACKUP_SHARED_GROUP}" "${BACKUP_LOCK_FILE}" 2>/dev/null || true
  chmod 660 "${BACKUP_LOCK_FILE}" 2>/dev/null || true
  exec 9<>"${BACKUP_LOCK_FILE}"
  flock -n 9 || fail "Backup atau restore lain sedang berjalan"
}

db_exec() {
  if [[ -n "${DATABASE_CONTAINER_NAME:-}" ]]; then
    docker exec -i "${DATABASE_CONTAINER_NAME}" "$@"
  else
    "${COMPOSE[@]}" exec -T database "$@"
  fi
}

require_database_ready() {
  db_exec pg_isready -U "${POSTGRES_ADMIN_USER}" -d "${POSTGRES_DATABASE}" >/dev/null \
    || fail "PostgreSQL tidak ready"
}

build_restic_docker_args() {
  RESTIC_DOCKER_ARGS=(
    run --rm
    -e "RESTIC_REPOSITORY=${RESTIC_REPOSITORY}"
    -e RESTIC_PASSWORD_FILE=/run/secrets/restic-password
    -e RESTIC_CACHE_DIR=/cache
    -v "${RESTIC_PASSWORD_FILE}:/run/secrets/restic-password:ro"
    -v "${RESTIC_CACHE_DIR}:/cache"
  )

  if [[ -n "${RESTIC_REPOSITORY_MOUNT:-}" ]]; then
    [[ "${RESTIC_REPOSITORY}" = /* ]] || fail "RESTIC_REPOSITORY harus absolute path saat RESTIC_REPOSITORY_MOUNT dipakai"
    mkdir -p "${RESTIC_REPOSITORY_MOUNT}"
    RESTIC_DOCKER_ARGS+=(-v "${RESTIC_REPOSITORY_MOUNT}:${RESTIC_REPOSITORY}")
  fi

  local variable
  for variable in AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_SESSION_TOKEN AWS_DEFAULT_REGION; do
    if [[ -n "${!variable:-}" ]]; then
      RESTIC_DOCKER_ARGS+=(-e "${variable}")
    fi
  done
}

restic_run() {
  build_restic_docker_args
  docker "${RESTIC_DOCKER_ARGS[@]}" "${RESTIC_IMAGE}" "$@"
}

read_metric_value() {
  local file="$1"
  local metric="$2"
  if [[ -f "${file}" ]]; then
    awk -v metric="${metric}" '$1 == metric { value=$2 } END { if (value != "") print value }' "${file}"
  fi
}

volume_stats() {
  local volume_name="${1:-${UPLOADS_VOLUME_NAME}}"
  docker run --rm -v "${volume_name}:/data:ro" "${ALPINE_IMAGE}" sh -c \
    "find /data -type f ! -name '*.tmp-*' -exec stat -c '%s' {} \; | awk '{count += 1; bytes += \$1} END {print count + 0, bytes + 0}'"
}

validate_bounded_value() {
  local name="$1"
  local value="$2"
  [[ "${value}" =~ ^[A-Za-z0-9._-]+$ ]] || fail "${name} hanya boleh berisi huruf, angka, titik, underscore, atau strip"
}
