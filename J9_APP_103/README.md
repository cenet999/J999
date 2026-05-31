## 一句话记住

- 日常开发：`pnpm dev`
- 安卓模拟器运行：`pnpm android`
- 安卓测试包：`pnpm build:android`，产物是 `apk`
- 安卓备用测试包：`pnpm build:android:preview`，产物是 `apk`
- 安卓商店包：`pnpm build:android:aab`，产物是 `aab`


## OTA升级

推荐用 **GitHub Actions** 发布（避免本地 ARM 机器 Hermes 编译失败）：

1. 在 [expo.dev/settings/access-tokens](https://expo.dev/settings/access-tokens) 创建 Token
2. 在 GitHub 仓库 **Settings → Secrets → Actions** 添加 `EXPO_TOKEN`
3. 推送 `J9_APP_103/` 的改动到 `main`，或在 Actions 页手动运行 **EAS Update (Production OTA)**

本地命令（x86 Mac/Linux 可用）：

- 生产环境：`pnpm update:prod`
- 预览环境：`pnpm update:preview`

首次初始化（只需执行一次，GitHub Actions 也会自动做）：

```bash
cd J9_APP_103
npx eas-cli update --branch production --environment production --message "初始化 production OTA"
npx eas-cli channel:edit production --branch production
```
