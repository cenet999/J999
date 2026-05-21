#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# 已加入 docker 组但当前终端会话未刷新附加组时（常见于 IDE 内终端），用 sg docker 重新执行本脚本
if ! docker info &>/dev/null; then
  if getent group docker | grep -qw "$(id -un)" && ! id -nG | grep -qw docker; then
    echo "当前 shell 未加载 docker 组，正在用 sg docker 重新执行..."
    _sh="${BASH:-/bin/bash}"
    exec sg docker -c "$(printf '%q ' "$_sh" "$0" "$@")"
  fi
fi

# 与 docker-compose.yaml 宿主机端口一致（本地 dotnet 常用 5231，可用 HOST_PORT=5231 ./docker-auto.sh）
HOST_PORT="${HOST_PORT:-8015}"

ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
IGNORE_SRC="$SCRIPT_DIR/root-context.dockerignore"
IGNORE_TARGET="$ROOT_DIR/.dockerignore"
IGNORE_BACKUP=""
restore_dockerignore() {
  if [[ -n "$IGNORE_BACKUP" && -f "$IGNORE_BACKUP" ]]; then
    mv -f "$IGNORE_BACKUP" "$IGNORE_TARGET"
  elif [[ -f "$IGNORE_TARGET" ]]; then
    rm -f "$IGNORE_TARGET"
  fi
}
if [[ -f "$IGNORE_TARGET" ]]; then
  IGNORE_BACKUP="$(mktemp)"
  cp "$IGNORE_TARGET" "$IGNORE_BACKUP"
fi
cp "$IGNORE_SRC" "$IGNORE_TARGET"
trap restore_dockerignore EXIT

mkdir -p Logs keys wwwroot/uploads wwwroot/avatars wwwroot/game wwwroot/badge-photos

for pid in $(sudo lsof -ti:"$HOST_PORT" 2>/dev/null); do
  sudo kill -9 "$pid" 2>/dev/null || true
done

docker compose build
docker compose down
docker compose up -d
