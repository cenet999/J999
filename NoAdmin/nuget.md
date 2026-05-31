## 重新封装

在仓库根目录查看并递增 `NoAdmin.Templates.csproj`、`NoAdmin.Blazor.csproj` 中的 `<Version>`，然后：

```bash
dotnet pack NoAdmin.Templates/NoAdmin.Templates.csproj -c Release
dotnet pack NoAdmin.Blazor/NoAdmin.Blazor.csproj -c Release
```

## 发布前测试

- `dotnet build` 成功

## 发布

使用环境变量传入 API Key（勿写入仓库）：

```bash
export NUGET_API_KEY="你的 NuGet API Key"

dotnet nuget push NoAdmin.Templates/bin/Release/NoAdmin.Templates.*.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json

dotnet nuget push NoAdmin.Blazor/bin/Release/NoAdmin.Blazor.*.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

NuGet 返回 409 表示版本已存在，请递增版本后重新 pack 再推送。

推送成功后，可执行 skill `review-then-git-push`。
