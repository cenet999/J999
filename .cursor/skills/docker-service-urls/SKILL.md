---
name: docker-service-urls
description: >
  通用：扫描所有运行中 Docker 容器的端口映射，输出 http://宿主机IP:端口 访问地址；默认使用非 127 的宿主机 IPv4，便于从其他机器访问。
  适用于任意容器（Web、API、数据库等）。在用户询问容器访问地址、docker 端口、外网/局域网打开服务、或需根据 docker ps 生成 URL 时使用。
---

# Docker 服务访问地址（通用，默认外机可访）

## 默认行为

- **基址**：使用宿主机上第一个**非 `127.*` 的 IPv4**。`DOCKER_URL_LOCAL=1` 时固定为 `127.0.0.1`。
- **通用扫描**：遍历**所有运行中容器**，用 `docker port` 获取端口映射，输出每个服务的 `http(s)://宿主机:端口`。容器内 443 使用 `https://`，其余使用 `http://`。

## 一键输出 URL（推荐）

```bash
bash .cursor/skills/docker-service-urls/scripts/docker_service_urls.sh
```

仅本机访问：

```bash
DOCKER_URL_LOCAL=1 bash .cursor/skills/docker-service-urls/scripts/docker_service_urls.sh
```

可选：尝试用默认浏览器打开所有列出的 URL（依赖 `xdg-open`）：

```bash
DOCKER_OPEN_BROWSER=1 bash .cursor/skills/docker-service-urls/scripts/docker_service_urls.sh
```

## 环境变量

| 变量 | 含义 | 默认 |
|------|------|------|
| `DOCKER_URL_LOCAL` | 使用 127.0.0.1 而非宿主机外网 IP | `0` |
| `DOCKER_OPEN_BROWSER` | 是否用 xdg-open 打开列出的 URL | `0` |

## 手动等价思路

1. `docker ps -q` 获取所有运行中容器 ID。
2. 对每个容器执行 `docker port <容器名或ID>`，解析宿主机端口。
3. 基址默认取 `hostname -I` 中第一个非 127 的 IPv4。
4. 输出 `http(s)://基址:端口`。

## 防火墙与安全组

从其他机器访问时，需在云厂商安全组或本机防火墙放行对应 TCP 端口。
