#!/usr/bin/env bash
# 释放 J9_APP_103 开发常用端口（默认 8081，Metro / expo start）。
# 不只杀占端口的进程，还会结束同项目下的 expo / metro / node 宿主，
# 避免端口刚释放又被 dev 脚本重新拉起。

set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PORT="${1:-8081}"
PROJECT_FILE="${ROOT}/package.json"
APP_NAME="$(basename "${ROOT}")"
EXPO_START_PATTERN="expo start.*${ROOT}|expo start -c"
METRO_PATTERN="metro.*${ROOT}|@expo/metro|metro-file-map"
NODE_PROJECT_PATTERN="node.*${ROOT}"
SELF_PID="$$"
PARENT_PID="${PPID:-}"

say() {
  echo "$1"
}

pid_exists() {
  local pid="$1"
  kill -0 "${pid}" 2>/dev/null
}

should_skip_pid() {
  local pid="$1"
  [[ -z "${pid}" ]] && return 0
  [[ "${pid}" == "${SELF_PID}" ]] && return 0
  [[ -n "${PARENT_PID}" ]] && [[ "${pid}" == "${PARENT_PID}" ]] && return 0
  return 1
}

pid_stat() {
  local pid="$1"
  ps -p "${pid}" -o stat= 2>/dev/null | tr -d ' ' || true
}

pid_args() {
  local pid="$1"
  ps -p "${pid}" -o args= 2>/dev/null || true
}

pid_ppid() {
  local pid="$1"
  ps -p "${pid}" -o ppid= 2>/dev/null | tr -d ' ' || true
}

pid_pgid() {
  local pid="$1"
  ps -p "${pid}" -o pgid= 2>/dev/null | tr -d ' ' || true
}

pid_cwd() {
  local pid="$1"
  local cwd_line
  cwd_line="$(lsof -a -p "${pid}" -d cwd 2>/dev/null | tail -1 || true)"
  echo "${cwd_line##* }"
}

kill_one() {
  local pid="$1"
  local reason="$2"
  local stat

  [[ -z "${pid}" ]] && return 0
  should_skip_pid "${pid}" && return 0
  pid_exists "${pid}" || return 0

  say "结束 PID ${pid}：${reason}"
  kill -TERM "${pid}" 2>/dev/null || true
  sleep 0.2

  if pid_exists "${pid}"; then
    kill -KILL "${pid}" 2>/dev/null || true
    sleep 0.3
  fi

  if pid_exists "${pid}"; then
    stat="$(pid_stat "${pid}")"
    say "PID ${pid} 还活着（状态: ${stat:-unknown}）"
  fi
}

kill_group() {
  local pid="$1"
  local pgid

  [[ -z "${pid}" ]] && return 0
  should_skip_pid "${pid}" && return 0
  pgid="$(pid_pgid "${pid}")"
  [[ -z "${pgid}" ]] && return 0
  [[ "${pgid}" == "0" ]] && return 0
  [[ "${pgid}" == "${pid}" ]] || true

  if ps -g "${pgid}" -o pid= 2>/dev/null | grep -q '[0-9]'; then
    say "结束进程组 ${pgid}（来源 PID ${pid}）"
    kill -TERM -"${pgid}" 2>/dev/null || true
    sleep 0.2
    kill -KILL -"${pgid}" 2>/dev/null || true
    sleep 0.3
  fi
}

kill_ancestor_chain() {
  local pid="$1"
  local depth=0
  local parent
  local args

  while [[ -n "${pid}" ]] && [[ "${pid}" != "0" ]] && [[ "${pid}" != "1" ]] && [[ "${depth}" -lt 12 ]]; do
    args="$(pid_args "${pid}")"
    if [[ "${args}" == *"${ROOT}"* ]] || [[ "${args}" == *"${PROJECT_FILE}"* ]] || [[ "${args}" == *"expo start"* ]] || [[ "${args}" == *"metro"* ]] || [[ "${args}" == *"${APP_NAME}"* ]]; then
      kill_one "${pid}" "项目相关宿主"
    fi
    parent="$(pid_ppid "${pid}")"
    [[ "${parent}" == "${pid}" ]] && break
    pid="${parent}"
    depth=$((depth + 1))
  done
}

kill_project_matches() {
  local pattern="$1"
  local reason="$2"
  local pid

  while IFS= read -r pid; do
    [[ -z "${pid}" ]] && continue
    should_skip_pid "${pid}" && continue
    kill_group "${pid}"
    kill_one "${pid}" "${reason}"
  done < <(pgrep -f "${pattern}" 2>/dev/null || true)
}

kill_project_cwd_processes() {
  local pid
  local cwd
  local args

  while IFS= read -r pid; do
    [[ -z "${pid}" ]] && continue
    should_skip_pid "${pid}" && continue
    cwd="$(pid_cwd "${pid}")"
    [[ "${cwd}" != "${ROOT}" ]] && continue
    args="$(pid_args "${pid}")"
    if [[ "${args}" == *"expo"* ]] || [[ "${args}" == *"metro"* ]] || [[ "${args}" == *"node"* ]] || [[ "${args}" == *"${APP_NAME}"* ]]; then
      kill_group "${pid}"
      kill_one "${pid}" "当前项目目录下的相关进程"
    fi
  done < <(pgrep -f "expo|metro|node" 2>/dev/null || true)
}

listeners() {
  lsof -tiTCP:"${PORT}" -sTCP:LISTEN 2>/dev/null | sort -u
}

wait_for_release() {
  local i
  for i in 1 2 3 4 5 6 7 8 9 10; do
    if ! lsof -iTCP:"${PORT}" -sTCP:LISTEN >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.3
  done
  return 1
}

print_still_busy_hint() {
  local pid
  say "端口 ${PORT} 还在被占用。当前监听进程："
  lsof -nP -iTCP:"${PORT}" -sTCP:LISTEN || true

  while IFS= read -r pid; do
    [[ -z "${pid}" ]] && continue
    say "PID ${pid} 状态：$(pid_stat "${pid}")"
    say "PID ${pid} 命令：$(pid_args "${pid}")"
  done < <(listeners)

  say "如果状态里有 U，说明这是系统层面卡住的进程，普通 kill 也不一定能立刻清掉。"
  say "这时最稳的办法是先保存手头工作，再重启电脑后重新执行 pnpm run dev。"
}

say "检查端口 ${PORT} …"

if lsof -iTCP:"${PORT}" -sTCP:LISTEN >/dev/null 2>&1; then
  while IFS= read -r pid; do
    [[ -z "${pid}" ]] && continue
    kill_group "${pid}"
    kill_ancestor_chain "${pid}"
    kill_one "${pid}" "直接监听 ${PORT} 的进程"
  done < <(listeners)
else
  say "当前无进程在 ${PORT} 上 LISTEN。"
fi

kill_project_matches "${EXPO_START_PATTERN}" "expo start 宿主"
kill_project_matches "${METRO_PATTERN}" "Metro 相关进程"
kill_project_matches "${NODE_PROJECT_PATTERN}" "项目目录 node 进程"
kill_project_cwd_processes

if wait_for_release; then
  say "端口 ${PORT} 已释放，可以重新执行 pnpm run dev。"
  exit 0
fi

print_still_busy_hint
exit 1
