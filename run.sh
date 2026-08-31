#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/Icewireless.AccountServiceDashboard.Web/Icewireless.AccountServiceDashboard.Web.csproj"
URLS="${URLS:-http://127.0.0.1:5088}"
PORT="$(echo "$URLS" | sed -E 's/.*:([0-9]+).*/\1/')"

cd "$ROOT"

if command -v lsof >/dev/null 2>&1; then
  PIDS="$(lsof -nP -iTCP:"$PORT" -sTCP:LISTEN -t 2>/dev/null || true)"
  if [[ -n "${PIDS}" ]]; then
    echo "Port $PORT in use (PIDs: $PIDS). Stopping..."
    # shellcheck disable=SC2086
    kill $PIDS 2>/dev/null || true
    sleep 1
  fi
fi

dotnet restore "$PROJECT"
dotnet build "$PROJECT" --no-restore -c Debug
echo "Starting at $URLS"
dotnet run --project "$PROJECT" --no-build --urls "$URLS"
