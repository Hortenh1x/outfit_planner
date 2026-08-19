#!/usr/bin/env bash
# Rotate secrets stored in the repo-root .env without ever printing their values.
#
# Usage:
#   tools/rotate-secret.sh list [--env-file PATH]
#   tools/rotate-secret.sh rotate KEY (--generate | --stdin) [--env-file PATH] [--no-verify] [--restart]
#
# New-value sources:
#   --generate   self-issued random secret (right for OBJECT_STORAGE_SIGNING_SECRET)
#   --stdin      paste a dashboard-issued credential; hidden prompt on a TTY, or pipe it in
#
# FASHN_API_KEY is validated against the FASHN credits endpoint before the swap
# (--no-verify skips that, e.g. when offline). After any dashboard-issued rotation
# the OLD credential must still be revoked at its issuer — no issuer exposes an API
# for that, so the script prints the right console URL as the final step.
#
# The previous .env is backed up next to it as .env.bak.<timestamp> (gitignored via
# the .env.* rule), rotations are logged to .secrets-rotation.log (key names and
# dates only, never values), and .env ends up with 600 permissions.
set -euo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(dirname -- "$SCRIPT_DIR")
ENV_FILE="$REPO_ROOT/.env"

MODE="${1:-}"
KEY=""
SOURCE=""
VERIFY=1
RESTART=0

usage() {
  sed -n '2,17p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

fail() {
  echo "error: $*" >&2
  exit 1
}

case "$MODE" in
  list) shift ;;
  rotate)
    shift
    KEY="${1:-}"
    [ -n "$KEY" ] || { usage; fail "rotate needs a KEY"; }
    shift
    ;;
  -h|--help|"") usage; exit 0 ;;
  *) usage; fail "unknown command: $MODE" ;;
esac

while [ $# -gt 0 ]; do
  case "$1" in
    --env-file) ENV_FILE="${2:?--env-file needs a path}"; shift 2 ;;
    --generate) SOURCE="generate"; shift ;;
    --stdin) SOURCE="stdin"; shift ;;
    --no-verify) VERIFY=0; shift ;;
    --restart) RESTART=1; shift ;;
    *) fail "unknown option: $1" ;;
  esac
done

[ -f "$ENV_FILE" ] || fail "env file not found: $ENV_FILE"
LOG_FILE="$(dirname -- "$ENV_FILE")/.secrets-rotation.log"

read_value() {
  awk -F= -v k="$1" '$1==k { print substr($0, length(k) + 2); exit }' "$ENV_FILE"
}

is_secret_name() {
  case "$1" in
    *SECRET*|*TOKEN*|*API_KEY*|*PASSWORD*) return 0 ;;
    *) return 1 ;;
  esac
}

if [ "$MODE" = "list" ]; then
  echo "Keys in ${ENV_FILE#"$REPO_ROOT"/} (values never shown):"
  while IFS= read -r line; do
    case "$line" in
      [A-Za-z_]*=*)
        key=${line%%=*}
        value=${line#*=}
        kind="config"
        is_secret_name "$key" && kind="SECRET"
        printf '  %-34s len=%-4s %s\n' "$key" "${#value}" "$kind"
        ;;
    esac
  done < "$ENV_FILE"
  if [ -f "$LOG_FILE" ]; then
    echo
    echo "Recent rotations (${LOG_FILE#"$REPO_ROOT"/}):"
    tail -n 5 "$LOG_FILE" | sed 's/^/  /'
  fi
  exit 0
fi

[ -n "$SOURCE" ] || fail "rotate needs --generate or --stdin"

case "$SOURCE" in
  generate)
    NEW_VALUE=$(openssl rand -base64 48 | tr '+/' '-_' | tr -d '=\n')
    ;;
  stdin)
    if [ -t 0 ]; then
      printf 'Paste the new value for %s (input hidden): ' "$KEY" >&2
      IFS= read -rs NEW_VALUE
      echo >&2
    else
      IFS= read -r NEW_VALUE
    fi
    ;;
esac

[ -n "$NEW_VALUE" ] || fail "new value is empty"
case "$NEW_VALUE" in
  *$'\n'*) fail "new value must be a single line" ;;
esac

OLD_VALUE=$(read_value "$KEY")
if [ -n "$OLD_VALUE" ]; then
  old_hash=$(printf '%s' "$OLD_VALUE" | sha256sum | cut -d' ' -f1)
  new_hash=$(printf '%s' "$NEW_VALUE" | sha256sum | cut -d' ' -f1)
  [ "$old_hash" != "$new_hash" ] || fail "the new value equals the current one — nothing rotated"
fi

# FASHN keys can be validated without spending credits: the credits endpoint
# returns 200 for a live key and 401/403 for a bad one.
if [ "$KEY" = "FASHN_API_KEY" ] && [ "$VERIFY" = "1" ]; then
  base=$(read_value "FASHN_BASE_URL")
  base=${base:-https://api.fashn.ai/v1/}
  base=${base%/}
  status=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 \
    -H "Authorization: Bearer $NEW_VALUE" "$base/credits" || echo "000")
  [ "$status" = "200" ] || fail "FASHN rejected the new key (HTTP $status) — not swapping. Use --no-verify to override."
  echo "FASHN accepted the new key (HTTP 200)."
fi

umask 077
STAMP=$(date +%Y%m%d-%H%M%S)
BACKUP="$ENV_FILE.bak.$STAMP"
cp -p "$ENV_FILE" "$BACKUP"
chmod 600 "$BACKUP"

TMP=$(mktemp "$ENV_FILE.XXXXXX")
replaced=0
while IFS= read -r line; do
  case "$line" in
    "$KEY="*)
      printf '%s=%s\n' "$KEY" "$NEW_VALUE" >> "$TMP"
      replaced=1
      ;;
    *)
      printf '%s\n' "$line" >> "$TMP"
      ;;
  esac
done < "$ENV_FILE"
if [ "$replaced" = "0" ]; then
  printf '%s=%s\n' "$KEY" "$NEW_VALUE" >> "$TMP"
fi
mv "$TMP" "$ENV_FILE"
chmod 600 "$ENV_FILE"

printf '%s key=%s method=%s len=%s\n' "$(date -Is)" "$KEY" "$SOURCE" "${#NEW_VALUE}" >> "$LOG_FILE"
chmod 600 "$LOG_FILE"

echo "Rotated $KEY (backup: ${BACKUP#"$REPO_ROOT"/})."

if [ "$RESTART" = "1" ]; then
  echo "Recreating the api service so it picks up the new value..."
  (cd "$REPO_ROOT" && docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml up -d api)
  echo "Waiting for the api health endpoint..."
  curl -k --retry 30 --retry-delay 2 --retry-connrefused -fsS https://localhost:5001/api/health >/dev/null \
    && echo "api is healthy." \
    || echo "warning: api health check did not pass — check 'docker compose logs api'." >&2
else
  echo "Note: running processes keep the OLD value until restarted (docker compose ... up -d api)."
fi

case "$KEY" in
  FASHN_API_KEY)
    echo "Final step: revoke the OLD key at https://app.fashn.ai (Settings -> API keys)." ;;
  GOOGLE_CLIENT_SECRET)
    echo "Final step: delete the OLD secret at https://console.cloud.google.com/apis/credentials (OAuth client -> client secrets)." ;;
  APPLE_CLIENT_SECRET)
    echo "Final step: Apple client secrets are short-lived JWTs signed with your Apple key — revoke the signing key at https://developer.apple.com/account/resources/authkeys/list if it leaked." ;;
  TUNNEL_TOKEN)
    echo "Final step: rotate/refresh the tunnel token in Cloudflare Zero Trust -> Networks -> Tunnels (and update the host cloudflared service if it runs outside compose)." ;;
  OBJECT_STORAGE_SIGNING_SECRET)
    echo "Self-issued secret: nothing to revoke. Outstanding signed URLs are invalid until re-signed on read (automatic)." ;;
esac
