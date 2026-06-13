# 构建上下文为 J999 仓库根目录，见 docker-compose.yaml
# 样式：仅使用仓库内 wwwroot/css/j9-admin.css（本地 npm run build:css 后提交），镜像内不安装 Node、不重编译 CSS
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["J9_NeoAdmin/J9_NeoAdmin.csproj", "J9_NeoAdmin/"]

RUN dotnet restore "J9_NeoAdmin/J9_NeoAdmin.csproj"

COPY J9_NeoAdmin/ J9_NeoAdmin/
WORKDIR /src/J9_NeoAdmin

FROM build AS publish
RUN dotnet publish "J9_NeoAdmin.csproj" -c Release -o /app/publish -p:SkipTailwindBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends locales && \
    sed -i '/en_US.UTF-8/s/^# //g' /etc/locale.gen && \
    sed -i '/zh_CN.UTF-8/s/^# //g' /etc/locale.gen && \
    locale-gen && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

RUN set -eux; \
    mkdir -p \
        /app/Logs \
        /app/wwwroot/uploads \
        /app/wwwroot/game \
        /app/wwwroot/avatars \
        /app/keys; \
    chmod -R 777 /app/Logs /app/wwwroot /app/keys

RUN rm -f /app/buyu.db || true

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV LANG=zh_CN.UTF-8
ENV LC_ALL=zh_CN.UTF-8
ENV LC_CTYPE=zh_CN.UTF-8

EXPOSE 80

ENTRYPOINT ["dotnet", "J9_NeoAdmin.dll"]
