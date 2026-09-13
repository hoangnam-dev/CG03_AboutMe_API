#!/usr/bin/env bash
set -Eeuo pipefail

if [[ ${SSH_ORIGINAL_COMMAND:-} =~ ^deploy\ ([0-9a-f]{40})$ ]]; then
  release_sha=${BASH_REMATCH[1]}
else
  echo "Rejected deployment command." >&2
  exit 64
fi

readonly release_sha
readonly image="ghcr.io/hoangnam-dev/cg03-aboutme-api:${release_sha}"
readonly production_name="cg03aboutme-api"
readonly candidate_name="cg03aboutme-api-candidate"
readonly previous_name="cg03aboutme-api-previous"
readonly network_name="cg03aboutme-prod"
readonly env_file="/opt/cg03aboutme-be/shared/app.env"
readonly pfx_file="/opt/cg03aboutme-be/shared/certificates/jwt-signing-2026-09.pfx"
readonly deployed_sha_file="/opt/cg03aboutme-be/shared/deployed-sha"
readonly public_ready_url="https://api.noveraxiv.com/health/ready"

cutover_started=false

log() {
  printf '[deploy] %s\n' "$*"
}

fail() {
  printf '[deploy] ERROR: %s\n' "$*" >&2
  return 1
}

container_exists() {
  docker container inspect "$1" >/dev/null 2>&1
}

local_health_is_ready() {
  local port=$1
  local path

  for path in /health /health/ready /health/supabase; do
    curl \
      --fail \
      --silent \
      --show-error \
      --connect-timeout 2 \
      --max-time 5 \
      --header 'X-Forwarded-Proto: https' \
      --output /dev/null \
      "http://127.0.0.1:${port}${path}" || return 1
  done
}

wait_for_local_health() {
  local port=$1
  local label=$2
  local attempt

  for attempt in $(seq 1 60); do
    if local_health_is_ready "$port"; then
      log "${label} passed all local health checks."
      return 0
    fi
    sleep 1
  done

  fail "${label} did not become healthy within 60 attempts."
}

wait_for_public_readiness() {
  local attempt

  for attempt in $(seq 1 30); do
    if curl \
      --fail \
      --silent \
      --show-error \
      --connect-timeout 3 \
      --max-time 10 \
      --output /dev/null \
      "$public_ready_url"; then
      log "Public readiness check passed."
      return 0
    fi
    sleep 1
  done

  fail "Public readiness did not become healthy within 30 attempts."
}

run_container() {
  local name=$1
  local host_port=$2
  shift 2

  docker run --detach \
    --name "$name" \
    --network "$network_name" \
    --env-file "$env_file" \
    --env ASPNETCORE_ENVIRONMENT=Production \
    --env 'ASPNETCORE_URLS=http://+:8080' \
    --mount "type=bind,src=${pfx_file},dst=/run/secrets/jwt-signing.pfx,readonly" \
    --publish "127.0.0.1:${host_port}:8080" \
    --security-opt no-new-privileges:true \
    "$@" \
    "$image"
}

rollback() {
  log "Rolling back to the previous production container."

  if container_exists "$production_name"; then
    docker logs --tail 100 "$production_name" >&2 || true
    docker rm --force "$production_name" >/dev/null || true
  fi

  if ! container_exists "$previous_name"; then
    log "Rollback container is unavailable; manual recovery is required."
    return 1
  fi

  docker rename "$previous_name" "$production_name"
  docker start "$production_name" >/dev/null
  wait_for_local_health 8080 "Rollback release"
  log "Rollback completed successfully."
}

handle_error() {
  local status=$1
  local line=$2
  trap - ERR

  printf '[deploy] Deployment failed at line %s with status %s.\n' "$line" "$status" >&2

  if [[ $cutover_started == true ]]; then
    rollback || true
  elif container_exists "$candidate_name"; then
    docker logs --tail 100 "$candidate_name" >&2 || true
    docker rm --force "$candidate_name" >/dev/null || true
  fi

  exit "$status"
}

trap 'handle_error "$?" "$LINENO"' ERR

command -v docker >/dev/null || fail "docker is unavailable."
command -v curl >/dev/null || fail "curl is unavailable."
command -v ss >/dev/null || fail "ss is unavailable."
docker network inspect "$network_name" >/dev/null 2>&1 || fail "Docker network ${network_name} is unavailable."
[[ -r $env_file ]] || fail "Environment file is not readable: ${env_file}"
[[ -r $pfx_file ]] || fail "JWT certificate is not readable: ${pfx_file}"
container_exists "$production_name" || fail "Production container ${production_name} does not exist."

if [[ -r $deployed_sha_file ]] && [[ $(<"$deployed_sha_file") == "$release_sha" ]]; then
  wait_for_local_health 8080 "Existing production release"
  wait_for_public_readiness
  log "Release ${release_sha} is already deployed."
  exit 0
fi

if container_exists "$candidate_name"; then
  log "Removing stale reserved candidate container."
  docker rm --force "$candidate_name" >/dev/null
fi

if ss -H -ltn 'sport = :8081' | grep -q .; then
  fail "Loopback port 8081 is already in use."
fi

log "Pulling ${image}."
docker pull "$image"

log "Starting candidate on loopback port 8081."
run_container "$candidate_name" 8081
wait_for_local_health 8081 "Candidate release"
docker stop "$candidate_name" >/dev/null
docker rm "$candidate_name" >/dev/null

if container_exists "$previous_name"; then
  if [[ $(docker inspect --format '{{.State.Running}}' "$previous_name") == true ]]; then
    fail "Reserved rollback container ${previous_name} is unexpectedly running."
  fi
  docker rm "$previous_name" >/dev/null
fi

log "Stopping current production container."
docker stop "$production_name" >/dev/null
if ! docker rename "$production_name" "$previous_name"; then
  docker start "$production_name" >/dev/null || true
  fail "Could not preserve the current production container."
  exit 1
fi
cutover_started=true

log "Starting release ${release_sha} on production port 8080."
run_container "$production_name" 8080 --restart unless-stopped
wait_for_local_health 8080 "Production release"
wait_for_public_readiness

printf '%s\n' "$release_sha" >"${deployed_sha_file}.tmp"
mv "${deployed_sha_file}.tmp" "$deployed_sha_file"

cutover_started=false
trap - ERR
log "Release ${release_sha} deployed successfully."
