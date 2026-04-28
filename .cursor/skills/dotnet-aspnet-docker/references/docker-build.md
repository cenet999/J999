# ASP.NET Core Docker 构建参考（通用模板）

本文档为**可拷贝模板**，将 `{CSPROJ}`、`{ASSEMBLY}` 等替换为当前项目即可。文末附 J9_Admin 对照表。

---

## 1. 文件清单（推荐最小集）

| 文件 | 作用 |
|------|------|
| `Dockerfile` | 多阶段构建 |
| `docker-compose.yaml` 或 `docker-compose.yml` | 构建上下文、端口、环境变量、卷 |
| `docker-auto.sh`（可选） | `build && down && up -d` 一键重建 |

---

## 2. Dockerfile 模板

说明：

- `{CSPROJ}`：例如 `MyApp.csproj`
- `{ASSEMBLY}`：发布后入口 DLL 名不含 `.dll`，通常与 csproj 默认程序集名一致
- 若使用**官方 NuGet**且网络正常，可整段删除 `dotnet nuget remove/add source`，仅保留 `dotnet restore`
- `mkdir` / `chmod` 仅保留应用**确实需要**的目录

```dockerfile
# 第一阶段：构建
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["{CSPROJ}", "./"]

# 可选：国内 NuGet 镜像（按需替换 URL 与名称）
RUN dotnet nuget remove source nuget.org || true
RUN dotnet nuget add source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json -n mirror_nuget
RUN dotnet restore "{CSPROJ}"

COPY . .
RUN dotnet build "{CSPROJ}" -c Release -o /app/build

# 第二阶段：发布
FROM build AS publish
RUN dotnet publish "{CSPROJ}" -c Release -o /app/publish

# 第三阶段：运行
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

# 按需创建可写目录（示例：日志、上传）
RUN mkdir -p /app/Logs /app/wwwroot/uploads && \
    chmod -R 777 /app/Logs /app/wwwroot/uploads

ENV ASPNETCORE_URLS=http://+:{CONTAINER_PORT}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8
ENV LC_CTYPE=C.UTF-8

EXPOSE {CONTAINER_PORT}

ENTRYPOINT ["dotnet", "{ASSEMBLY}.dll"]
```

**.NET 版本**：将 `8.0` 改为 `9.0` 等与项目 `TargetFramework` 一致。

**监听端口**：若应用固定用 8080，将 `{CONTAINER_PORT}` 改为 `8080`，并与 `ASPNETCORE_URLS` 一致。

---

## 3. docker-compose 模板

将 `{SERVICE}`、`{CONTAINER}`、`{HOST_PORT}`、`{CONTAINER_PORT}` 及 `volumes` 按项目调整。

```yaml
services:
  {SERVICE}:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: {CONTAINER}
    ports:
      - "{HOST_PORT}:{CONTAINER_PORT}"
    environment:
      - TZ=Asia/Shanghai
    volumes:
      # 以下为常见项，按实际增删
      - ./Logs:/app/Logs
      - ./wwwroot/uploads:/app/wwwroot/uploads
      - ./keys:/app/keys
      # - ./data/app.db:/app/app.db
      - /etc/localtime:/etc/localtime:ro
      - /etc/timezone:/etc/timezone:ro
    restart: unless-stopped
```

**多服务**：在同一文件中增加 `db`、`redis` 等，并在 Web 服务上通过 `depends_on`、连接字符串环境变量关联。

---

## 4. docker-auto.sh 模板

```bash
#!/usr/bin/env bash
set -e
# 可选：释放本机占用端口（将 5231 改为你的冲突端口；不需要则删除）
# fuser -k 5231/tcp 2>/dev/null || true

docker compose build
docker compose down
docker compose up -d
```

使用 `docker-compose` 旧 CLI 时，将三处改为 `docker-compose`。

---

## 5. 手动构建与运行

```bash
docker build -t {IMAGE_TAG} .

docker run -d -p {HOST_PORT}:{CONTAINER_PORT} \
  -e TZ=Asia/Shanghai \
  --name {CONTAINER} \
  {IMAGE_TAG}
```

带卷时追加 `-v 宿主机路径:容器路径`。

---

## 6. 解决方案多项目

- **构建上下文**：`context` 设为包含 `.sln` 及所有被引用项目的目录（常为解决方案根或 Web 项目上级目录）。
- **Dockerfile**：`COPY` 需能复制到所有依赖项目；可先 `COPY *.sln`、`COPY src/MyLib/MyLib.csproj` 等分层以利用缓存，再 `COPY . .`。
- **发布**：`dotnet publish path/to/Web.csproj` 指定 Web 入口项目。

---

## 7. 故障排查（通用）

| 现象 | 处理方向 |
|------|----------|
| restore 超时/失败 | 换 NuGet 源或恢复官方源；检查代理 |
| 容器启动即退出 | 查看 `docker logs {CONTAINER}`；检查 DLL 名与 `ENTRYPOINT` |
| 端口冲突 | 改 `ports` 左侧宿主机端口 |
| 数据库/文件找不到 | 确认卷路径存在；SQLite 可先本地运行生成或挂载空目录策略与文档一致 |
| 权限 denied | 宿主机目录权限或 Dockerfile 中 `chmod`/运行用户 |

---

## 8. 本仓库 J9_Admin 对照（实例）

| 占位符 | 取值 |
|--------|------|
| `{PROJECT_DIR}` | `J9_Admin` |
| `{CSPROJ}` | `J9_Admin.csproj` |
| `{ASSEMBLY}` | `J9_Admin` |
| `{SERVICE}` | `buyu-game-backend`（历史命名，可改为 `api`） |
| `{CONTAINER}` | `j9-admin-backend` |
| `{HOST_PORT}` | `8015` |
| `{CONTAINER_PORT}` | `80` |
| 数据库卷 | `./buyu.db:/app/buyu.db` |

J9 当前 Dockerfile 使用 `dotnet nuget remove source nuget.org`（无 `|| true`）；若机器上未配置过该源，构建可能报错，通用模板中已用 `|| true` 更稳妥，迁移时可按需保留原行为。
