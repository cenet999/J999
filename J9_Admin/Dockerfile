# 第一阶段：构建阶段
# 构建上下文为仓库根目录（含 J999 与 NoAdmin），见 docker-compose.yaml
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 先复制项目文件，充分利用 Docker 缓存
COPY ["J999/J9_Admin/J9_Admin.csproj", "J999/J9_Admin/"]
COPY ["NoAdmin/NoAdmin.Blazor/NoAdmin.Blazor.csproj", "NoAdmin/NoAdmin.Blazor/"]

# 还原包（含 ProjectReference）
RUN dotnet restore "J999/J9_Admin/J9_Admin.csproj"

# 复制源代码
COPY J999/J9_Admin/ J999/J9_Admin/
COPY NoAdmin/NoAdmin.Blazor/ NoAdmin/NoAdmin.Blazor/

WORKDIR /src/J999/J9_Admin

# 发布（内含 build，会正确编译 NoAdmin.Blazor 等 ProjectReference）
RUN dotnet publish "J9_Admin.csproj" -c Release -o /app/publish --no-restore

# 第二阶段：运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 安装多语言支持
RUN apt-get update && \
    apt-get install -y --no-install-recommends locales && \
    sed -i '/en_US.UTF-8/s/^# //g' /etc/locale.gen && \
    sed -i '/zh_CN.UTF-8/s/^# //g' /etc/locale.gen && \
    locale-gen && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# 从发布阶段复制文件
COPY --from=build /app/publish .

# 创建必要的目录并设置权限
RUN set -eux; \
    mkdir -p \
        /app/Logs \
        /app/wwwroot/uploads \
        /app/wwwroot/game \
        /app/wwwroot/avatars \
        /app/wwwroot/badge-photos \
        /app/keys; \
    chmod -R 777 /app/Logs /app/wwwroot /app/keys

# buyu.db：由 docker-compose 将宿主机 ./buyu.db 绑定到 /app/buyu.db（单文件，勿在镜像内持久化）
RUN rm -f /app/buyu.db || true

# 设置环境变量
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
# 多语言环境变量设置（默认使用中文 UTF-8）
ENV LANG=zh_CN.UTF-8
ENV LC_ALL=zh_CN.UTF-8
ENV LC_CTYPE=zh_CN.UTF-8

# 暴露端口
EXPOSE 80

# 启动应用
ENTRYPOINT ["dotnet", "J9_Admin.dll"]
