#!/usr/bin/env bash
# 自动拉取仓库最新代码，若有更新则触发对应子项目的 docker-auto.sh
# 用法:
#   autopull.sh             # cron 模式：无新提交时直接退出，只对有变更的项目重启容器
#   autopull.sh --force     # 强制模式：无论有无新提交，都重跑两个 docker-auto.sh
#   autopull.sh -f          # 同 --force
# 若在 TTY 下手动运行（交互终端），默认自动开启 --force。
#
# 建议通过 cron 每 10 分钟执行一次（请改成你机器上的真实路径）：
#   */10 * * * * /root/dd/J999/autopull.sh >> /root/dd/J999/autopull.log 2>&1
# 查看crontab: crontab -l
#
# 每次运行开始会按「日志行日期」裁剪 autopull.log：仅保留最近 3 天（含 cutoff
# 当天）以内内容；无 [YYYY-MM-DD ...] 前缀的行归属其上一条带日期的 log 块。
#
# 注意：cron 用「>> 日志」启动时，shell 已打开旧 inode；prune 里 mv 覆盖路径后
# 必须 exec 重新挂接 stdout，否则后续 log 全写到已删除的 inode 里，文件看起来
#「很久没更新」。
set -uo pipefail

# cron 环境 PATH 较窄，这里显式补齐 docker/git/sg 等常用路径
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${PATH:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="${SCRIPT_DIR}"
LOG_FILE="${REPO_DIR}/autopull.log"
LOCK_FILE="${REPO_DIR}/.autopull.lock"
BRANCH="main"

FORCE=0
for arg in "$@"; do
  case "${arg}" in
    -f|--force) FORCE=1 ;;
    -h|--help)
      sed -n '2,9p' "$0"
      exit 0
      ;;
  esac
done

# TTY 环境视为手动运行，自动启用 force
if [[ "${FORCE}" -eq 0 && -t 1 ]]; then
  FORCE=1
  MANUAL_TTY=1
else
  MANUAL_TTY=0
fi

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"
}

# 仅保留 LOG_FILE 中「行首日期」不早于 (今天 − 3 天) 的记录；docker 等多行输出
# 紧跟在带日期的 log 行后，会一并保留或丢弃。
prune_autopull_log() {
  local f="${LOG_FILE}"
  [[ -f "${f}" ]] || return 0
  [[ -s "${f}" ]] || return 0

  local cutoff
  if cutoff=$(date -v-3d '+%Y-%m-%d' 2>/dev/null); then
    :
  else
    cutoff=$(date -d '3 days ago' '+%Y-%m-%d')
  fi

  local tmp
  tmp="$(mktemp "${REPO_DIR}/.autopull.log.prune.XXXXXX")" || return 1
  AUTOPULL_LOG_CUTOFF_DATE="${cutoff}" awk '
    BEGIN { cutoff = ENVIRON["AUTOPULL_LOG_CUTOFF_DATE"] }
    /^\[[0-9]{4}-[0-9]{2}-[0-9]{2}/ {
      d = substr($0, 2, 10)
      if (d >= cutoff) { recent = 1; print }
      else { recent = 0 }
      next
    }
    recent { print }
  ' "${f}" > "${tmp}" || {
    rm -f "${tmp}"
    return 1
  }
  mv "${tmp}" "${f}" || {
    rm -f "${tmp}"
    return 1
  }
  chmod 0644 "${f}" 2>/dev/null || true
}

# 通过 flock 防止重叠执行（上一次还没跑完时直接跳过本次）
if ! exec 9>"${LOCK_FILE}"; then
  log "无法创建锁文件: ${LOCK_FILE}"
  exit 1
fi
if ! flock -n 9; then
  log "已有 autopull 在运行，跳过本次"
  exit 0
fi

# 日志由 root cron 重定向写入，默认常为 600；放宽为 644 便于 SSH 上的部署用户在编辑器中查看
if [[ -f "${LOG_FILE}" ]]; then
  chmod 0644 "${LOG_FILE}" 2>/dev/null || true
fi

cd "${REPO_DIR}"

# root/cron 下仓库目录属主与当前用户不一致时，Git 2.35+ 会拒绝操作（dubious ownership）
if ! git config --global --get-all safe.directory 2>/dev/null | grep -Fxq "${REPO_DIR}"; then
  git config --global --add safe.directory "${REPO_DIR}"
fi

prune_autopull_log || log "警告: 裁剪 ${LOG_FILE} 失败，保留原日志"

# 重新打开日志路径，避免 cron 外层 >> 仍指向 prune 前的旧 inode（见文件头说明）
exec >>"${LOG_FILE}" 2>&1

if [[ "${FORCE}" -eq 1 ]]; then
  if [[ "${MANUAL_TTY}" -eq 1 ]]; then
    log "手动运行 (TTY)，已启用强制模式"
  else
    log "已启用强制模式 (--force)"
  fi
fi

log "开始拉取: ${REPO_DIR} (${BRANCH})"

OLD_HEAD="$(git rev-parse HEAD 2>/dev/null || echo '')"

if ! git fetch --quiet origin "${BRANCH}"; then
  log "git fetch 失败，退出"
  exit 1
fi

NEW_HEAD="$(git rev-parse "origin/${BRANCH}")"

if [[ "${OLD_HEAD}" == "${NEW_HEAD}" ]]; then
  if [[ "${FORCE}" -eq 1 ]]; then
    log "无新提交，当前 HEAD=${OLD_HEAD:0:7}；强制模式继续执行"
  else
    log "无新提交，当前 HEAD=${OLD_HEAD:0:7}"
    exit 0
  fi
else
  log "发现新提交: ${OLD_HEAD:0:7} -> ${NEW_HEAD:0:7}"
  # 使用 reset --hard 强制与远端一致，避免本地脏目录导致 pull 失败
  if ! git reset --hard "origin/${BRANCH}" >/dev/null; then
    log "git reset --hard 失败，退出"
    exit 1
  fi
fi

# 判断哪些子项目需要重启：
# - 强制模式：两个都跑
# - 自动模式：按 git diff 的改动路径决定
NEED_ADMIN=0
NEED_APP=0
if [[ "${FORCE}" -eq 1 ]]; then
  NEED_ADMIN=1
  NEED_APP=1
else
  CHANGED_FILES="$(git diff --name-only "${OLD_HEAD}" "${NEW_HEAD}" || true)"
  if echo "${CHANGED_FILES}" | grep -q '^J9_Admin/'; then
    NEED_ADMIN=1
  fi
  if echo "${CHANGED_FILES}" | grep -q '^J9_APP_103/'; then
    NEED_APP=1
  fi
fi

run_docker_auto() {
  local name="$1"
  local dir="${REPO_DIR}/${name}"
  local script="${dir}/docker-auto.sh"

  if [[ ! -x "${script}" ]]; then
    log "跳过 ${name}: 未找到可执行脚本 ${script}"
    return
  fi

  log "执行 ${name}/docker-auto.sh ..."
  echo "---------- [${name}] docker-auto.sh output begin ----------"
  local rc=0
  (cd "${dir}" && bash "${script}") || rc=$?
  echo "---------- [${name}] docker-auto.sh output end   ----------"
  if [[ "${rc}" -eq 0 ]]; then
    log "${name} 部署完成"
  else
    log "${name} 部署失败 (exit=${rc})"
  fi
}

if [[ "${NEED_ADMIN}" -eq 1 ]]; then
  run_docker_auto "J9_Admin"
else
  log "J9_Admin 无变更，跳过"
fi

if [[ "${NEED_APP}" -eq 1 ]]; then
  run_docker_auto "J9_APP_103"
else
  log "J9_APP_103 无变更，跳过"
fi

log "本次任务完成"
