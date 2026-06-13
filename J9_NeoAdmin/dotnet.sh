#!/usr/bin/env bash
# 启动前预置 demo 演示账号，避免 NeoAdmin UserSeedData 与 DMember 扩展字段冲突
# set -euo pipefail
# SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# cd "$SCRIPT_DIR"
# dotnet run -- seed-sysuser-demo
dotnet watch run
