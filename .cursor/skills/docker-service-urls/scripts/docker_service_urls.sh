#!/usr/bin/env bash
# 通用：扫描所有运行中 Docker 容器端口映射，输出 http(s)://宿主机:端口。
# 默认宿主机 IP 供局域网/外网访问；DOCKER_URL_LOCAL=1 时用 127.0.0.1。
set -euo pipefail

resolve_host() {
  if [[ "${DOCKER_URL_LOCAL:-0}" == "1" ]]; then
    echo "127.0.0.1"
    return
  fi
  local line ip
  line=$(hostname -I 2>/dev/null || true)
  for ip in $line; do
    if [[ "$ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] && [[ ! "$ip" =~ ^127\. ]]; then
      echo "$ip"
      return
    fi
  done
  echo "127.0.0.1"
}

# 从 "80/tcp -> 0.0.0.0:8015" 或 "443/tcp -> [::]:8443" 提取 容器端口 和 宿主机端口
parse_port_line() {
  local line="$1"
  local cport hport
  cport=$(echo "$line" | sed -nE 's|^([0-9]+)/.*|\1|p')
  hport=$(echo "$line" | sed -nE 's|.*:([0-9]+)$|\1|p')
  if [[ -n "$cport" && -n "$hport" ]]; then
    echo "${cport} ${hport}"
  fi
}

HOST=$(resolve_host)
OPEN_BROWSER="${DOCKER_OPEN_BROWSER:-0}"
URLS=()

echo "宿主机: $HOST （DOCKER_URL_LOCAL=1 时为 127.0.0.1）"
echo ""

while read -r cid; do
  [[ -z "$cid" ]] && continue
  name=$(docker inspect -f '{{.Name}}' "$cid" 2>/dev/null | sed 's/^\///' || echo "$cid")
  while read -r pline; do
    [[ -z "$pline" ]] && continue
    parsed=$(parse_port_line "$pline")
    [[ -z "$parsed" ]] && continue
    read -r cport hport <<< "$parsed"
    if [[ "$cport" == "443" ]]; then
      url="https://${HOST}:${hport}"
    else
      url="http://${HOST}:${hport}"
    fi
    printf "%-24s %6s  %s\n" "$name" "${cport}/tcp" "$url"
    URLS+=("$url")
  done < <(docker port "$cid" 2>/dev/null || true)
done < <(docker ps -q 2>/dev/null || true)

if [[ "${#URLS[@]}" -eq 0 ]]; then
  echo "（无运行中容器，或均未映射端口）"
fi

if [[ "$OPEN_BROWSER" == "1" && "${#URLS[@]}" -gt 0 ]]; then
  for u in "${URLS[@]}"; do
    xdg-open "$u" 2>/dev/null || true
  done
fi
