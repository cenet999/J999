#!/usr/bin/env bash
# PostgreSQL -> 本地 SQLite(buyu.db) 数据同步
# PG 与 Sqlite 连接串：同步时仅读 appsettings.json + 环境变量（不读 appsettings.Development.json）。
#       本地 buyu.db 已存在且表结构齐全（可先执行一次 dotnet run 由 FreeSql 建表）。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

usage() {
  cat <<'EOF'
用法:
  ./sync-pg-to-sqlite.sh           执行同步（会先隐式编译如需）
  ./sync-pg-to-sqlite.sh sync      同上
  ./sync-pg-to-sqlite.sh build     先 dotnet build 再同步（推荐 CI/确认编译通过时用）

环境变量示例（可不写进 appsettings）:
  export ConnectionStrings__PostgreSQL__Default='Host=...;Username=...;Password=...;...'

说明:
  同步时连接串见 appsettings.json；Program.cs 首参数 sync-pg-to-sqlite 与 PostgreSqlToSqliteSyncRunner。
EOF
}

case "${1:-sync}" in
  -h|--help|help)
    usage
    exit 0
    ;;
  sync)
    exec dotnet run -- sync-pg-to-sqlite
    ;;
  build)
    dotnet build
    exec dotnet run --no-build -- sync-pg-to-sqlite
    ;;
  *)
    echo "未知子命令: $1" >&2
    usage >&2
    exit 1
    ;;
esac
