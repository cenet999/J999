# AGENTS.md

## 约定
- 在 Codex 中开启新的会话时，默认先使用内置浏览器打开 `http://localhost:5038/`。
- 如果该地址不可访问，请先确认本地服务是否已启动，再继续后续排查。
- 除非用户明确要求其他入口，否则后续与本仓库相关的检查优先围绕这个本地地址展开。
- 使用 node 或 python3 代替 python 执行脚本。

## 发布

在仓库根目录执行 `dotnet pack -c Release` 后，使用环境变量中的 API Key 推送（勿将 Key 写入仓库）：

```bash
export NUGET_API_KEY="你的 NuGet API Key"

dotnet nuget push NoAdmin.Templates/bin/Release/NoAdmin.Templates.*.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push NoAdmin.Blazor/bin/Release/NoAdmin.Blazor.*.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

NuGet 返回 409 表示版本已存在，请递增 `NoAdmin.Templates.csproj` / `NoAdmin.Blazor.csproj` 中的 `<Version>` 后重新 pack 再推送。

推送成功后，可执行 skill `review-then-git-push`。
