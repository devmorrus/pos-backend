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
    HEALTHCHECK_URL="http://127.0.0.1:8085/api/public/categories"
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
    docker compose logs --tail=120 backend || true
  fi

  exit "${exit_code}"
}

trap finish_deploy EXIT

echo "Deploying ${TARGET_ENVIRONMENT} using ref ${DEPLOY_REF}"
cd "${APP_PATH}"

git_fetch_for_deploy

if [[ -n "${DEPLOY_REF_INPUT}" ]] && git show-ref --verify --quiet "refs/remotes/origin/${DEPLOY_REF_INPUT}"; then
  DEPLOY_REF="origin/${DEPLOY_REF_INPUT}"
elif [[ -n "${DEPLOY_REF_INPUT}" ]] && git show-ref --verify --quiet "refs/tags/${DEPLOY_REF_INPUT}"; then
  DEPLOY_REF="refs/tags/${DEPLOY_REF_INPUT}"
fi

git checkout -f "${TARGET_BRANCH}"
git reset --hard "${DEPLOY_REF}"

docker compose up -d database
docker compose build backend
docker compose up -d --no-deps backend
docker compose ps

curl --fail --silent --show-error --location \
  --retry 10 \
  --retry-delay 5 \
  --max-time 30 \
  "${HEALTHCHECK_URL}" >/dev/null
