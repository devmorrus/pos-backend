#!/usr/bin/env bash

set -euo pipefail

TARGET_ENVIRONMENT="${1:-}"
DEPLOY_REF_INPUT="${2:-}"

if [[ -z "${TARGET_ENVIRONMENT}" ]]; then
  echo "Usage: ./deploy.sh [production] [optional-ref]"
  exit 1
fi

case "${TARGET_ENVIRONMENT}" in
  production)
    APP_PATH="/home/morrusdigital/pos-backend"
    TARGET_BRANCH="main"
    HEALTHCHECK_URL="http://127.0.0.1:8085/health/ready"
    ;;
  *)
    echo "Unknown environment: ${TARGET_ENVIRONMENT}"
    exit 1
    ;;
esac

DEPLOY_REF="${DEPLOY_REF_INPUT:-origin/${TARGET_BRANCH}}"

git_fetch_for_deploy() {
  local attempt
  local max_attempts=3

  for ((attempt = 1; attempt <= max_attempts; attempt++)); do
    if [[ -z "${DEPLOY_REF_INPUT}" ]]; then
      if git -c pack.threads=1 -c fetch.unpackLimit=1 fetch --prune --no-tags origin "${TARGET_BRANCH}"; then
        return 0
      fi
    elif git -c pack.threads=1 -c fetch.unpackLimit=1 fetch origin --prune --tags; then
      return 0
    fi

    echo "git fetch failed (attempt ${attempt}/${max_attempts})." >&2

    if [[ "${attempt}" -lt "${max_attempts}" ]]; then
      sleep $((attempt * 5))
    fi
  done

  return 1
}

finish_deploy() {
  local exit_code=$?
  trap - EXIT

  if [[ "${exit_code}" -eq 0 ]]; then
    echo "Deployment completed for ${TARGET_ENVIRONMENT}"
  else
    echo "Deployment failed for ${TARGET_ENVIRONMENT}." >&2
    docker compose -f docker-compose.yml -f ops/observability/docker-compose.observability.yml \
      logs --tail=120 backend otel-collector || true
  fi

  exit "${exit_code}"
}

trap finish_deploy EXIT

echo "Deploying ${TARGET_ENVIRONMENT} using ref ${DEPLOY_REF}"
cd "${APP_PATH}"
DEPLOY_STATE_DIR="${APP_PATH}/.deploy-state"
DEPLOYED_REF_FILE="${DEPLOY_STATE_DIR}/deployed-ref"

: "${ALERT_WEBHOOK_FILE:=./ops/observability/secrets/alert_webhook_url}"
if [[ ! -s "${ALERT_WEBHOOK_FILE}" ]]; then
  echo "Alert webhook secret file tidak ditemukan atau kosong: ${ALERT_WEBHOOK_FILE}" >&2
  exit 1
fi
export OBSERVABILITY_ENABLED="${OBSERVABILITY_ENABLED:-true}"

COMPOSE=(docker compose -f docker-compose.yml -f ops/observability/docker-compose.observability.yml)

git_fetch_for_deploy

if [[ -n "${DEPLOY_REF_INPUT}" ]] && git show-ref --verify --quiet "refs/remotes/origin/${DEPLOY_REF_INPUT}"; then
  DEPLOY_REF="origin/${DEPLOY_REF_INPUT}"
elif [[ -n "${DEPLOY_REF_INPUT}" ]] && git show-ref --verify --quiet "refs/tags/${DEPLOY_REF_INPUT}"; then
  DEPLOY_REF="refs/tags/${DEPLOY_REF_INPUT}"
fi

TARGET_REF_RESOLVED="$(git rev-parse "${DEPLOY_REF}^{commit}")"
TARGET_VERSION="$(git rev-parse --short "${TARGET_REF_RESOLVED}")"
DATABASE_CHANGE=true
if [[ -s "${DEPLOYED_REF_FILE}" ]] && git cat-file -e "$(cat "${DEPLOYED_REF_FILE}")^{commit}" 2>/dev/null; then
  LAST_DEPLOYED_REF="$(cat "${DEPLOYED_REF_FILE}")"
  if git diff --quiet "${LAST_DEPLOYED_REF}" "${TARGET_REF_RESOLVED}" -- \
    src/MorrusPOS.Infrastructure/Migrations \
    src/MorrusPOS.Infrastructure/Persistence/Migrations \
    db/tenant-roles.sql; then
    DATABASE_CHANGE=false
  fi
else
  echo "Deployed ref state belum tersedia; fresh pre-deploy backup diwajibkan."
fi

"${COMPOSE[@]}" up -d database

for attempt in {1..30}; do
  if "${COMPOSE[@]}" exec -T database pg_isready -U morrus -d morrusposs >/dev/null; then
    break
  fi
  if [[ "${attempt}" -eq 30 ]]; then
    echo "Database did not become ready." >&2
    exit 1
  fi
  sleep 2
done

if [[ "${DATABASE_CHANGE}" == "true" ]]; then
  BACKUP_SOURCE_SERVICE_VERSION="${LAST_DEPLOYED_REF:-unknown}" \
    bash scripts/backup/create-backup.sh "pre-deploy-${TARGET_VERSION}"
else
  bash scripts/backup/check-backup-freshness.sh
fi

git checkout -f "${TARGET_BRANCH}"
git reset --hard "${DEPLOY_REF}"
export SERVICE_VERSION="${TARGET_VERSION}"

# Init scripts only run on a new PostgreSQL volume. Re-apply role ownership and
# grants on every deployment so existing production volumes are upgraded too.
"${COMPOSE[@]}" exec -T database psql -U morrus -d morrusposs -v ON_ERROR_STOP=1 \
  < db/tenant-roles.sql

"${COMPOSE[@]}" up -d loki tempo alertmanager otel-collector prometheus grafana
"${COMPOSE[@]}" build backend
"${COMPOSE[@]}" up -d --no-deps backend
"${COMPOSE[@]}" ps

for attempt in {1..30}; do
  if curl --fail --silent --show-error --location --max-time 10 "${HEALTHCHECK_URL}" >/dev/null; then
    "${COMPOSE[@]}" exec -T database psql -U morrus -d morrusposs -v ON_ERROR_STOP=1 \
      < db/tenant-verify.sql
    bash scripts/check-platform-health.sh
    mkdir -p "${DEPLOY_STATE_DIR}"
    printf '%s\n' "${TARGET_REF_RESOLVED}" > "${DEPLOYED_REF_FILE}.tmp"
    mv "${DEPLOYED_REF_FILE}.tmp" "${DEPLOYED_REF_FILE}"
    echo "Health check passed."
    exit 0
  fi

  echo "Health check failed (attempt ${attempt}/30), retrying in 5s..."
  sleep 5
done

echo "Health check failed after 30 attempts." >&2
exit 1
