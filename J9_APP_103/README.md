## 一句话记住

- 日常开发：`pnpm dev`
- 安卓模拟器运行：`pnpm android`
- 安卓测试包：`pnpm build:android`，产物是 `apk`
- 安卓备用测试包：`pnpm build:android:preview`，产物是 `apk`
- 安卓商店包：`pnpm build:android:aab`，产物是 `aab`


## OTA升级

- 生产环境：`pnpm update:prod`
- 预览环境：`pnpm update:preview`

如需自定义说明，可直接改 `package.json` 里的 `--message`，或执行：

```bash
npx eas-cli update --channel production --message "你的更新说明"
```


cd /root/dd/J999/J9_APP_103

# ① 创建 production 分支并发布第一次 OTA
npx eas-cli update --branch production --environment production --message "初始化 production OTA"

# ② 把 channel 绑定到 branch（否则已安装的 App 收不到更新）
npx eas-cli channel:edit production --branch production

pnpm update:prod
