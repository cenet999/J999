---
name: dotnet-aspnet-docker
description: >
  ASP.NET Core（.NET 8+）Web 应用的多阶段 Docker 构建与 docker-compose 部署通用流程。
  适用于：新建或调整 Dockerfile、compose、一键重建脚本、NuGet 镜像源、端口与卷挂载、
  生产环境变量。当项目为 .NET Web（Blazor/API）且需容器化时使用；本仓库 J9_Admin 为实例之一。
---

# ASP.NET Core Web 应用 Docker 通用构建

## 概述

标准流程：**多阶段构建**（SDK 还原/编译 → publish → aspnet runtime 运行）+ **docker-compose** 编排持久化目录与时区。

将下列占位符替换为当前项目实际值：

| 占位符 | 含义 | 示例（J9_Admin） |
|--------|------|------------------|
| `{PROJECT_DIR}` | 含 Dockerfile 的项目根目录 | `J9_Admin` |
| `{CSPROJ}` | 入口 Web 项目文件名 | `J9_Admin.csproj` |
| `{ASSEMBLY}` | 程序集名（无扩展名，与 csproj 中 `AssemblyName` 或默认项目名一致） | `J9_Admin` |
| `{IMAGE_TAG}` | 镜像标签 | `myapp-api` |
| `{SERVICE}` | compose 服务名 | `api` |
| `{CONTAINER}` | 容器名 | `myapp-backend` |
| `{HOST_PORT}` | 宿主机映射端口 | `8015` |
| `{CONTAINER_PORT}` | 应用监听端口（与 `ASPNETCORE_URLS` 一致） | `80` |

## 快速命令

```bash
cd {PROJECT_DIR}

docker compose up              # 前台启动
docker compose up -d           # 后台启动

# 完整重建并重启（与典型 docker-auto.sh 一致）
docker compose build && docker compose down && docker compose up -d
```

## 构建阶段（固定模式）

1. **build**：`sdk` 镜像，`COPY` csproj → `dotnet restore` → `COPY` 源码 → `dotnet build`
2. **publish**：`dotnet publish -c Release -o /app/publish`
3. **final**：`aspnet` 镜像，`COPY --from=publish`，`ENTRYPOINT dotnet {ASSEMBLY}.dll`

详见 [references/docker-build.md](references/docker-build.md) 中的模板与 compose 卷清单。

## 按项目裁剪清单

- **NuGet**：国内网络可换镜像源；可访问 nuget.org 时可删除 `remove source` / `add source` 段
- **持久化卷**：仅挂载应用真实写入的路径（日志、上传、数据库文件、密钥目录等）
- **docker-auto.sh**：若需释放本地端口，把脚本里的端口号改成你本机冲突的端口，或删除该行
- **多项目解决方案**：`COPY` 与 `dotnet publish` 指向**启动用的 Web 项目** csproj；若依赖其他项目，构建上下文需包含整个 solution 目录

## 本仓库实例

九游后端 **J9_Admin** 使用本流程：端口 `8015`、卷含 `Logs`、`wwwroot/uploads`、`keys`、`buyu.db`。具体文件名见该目录下 `Dockerfile` 与 `docker-compose.yaml`。
