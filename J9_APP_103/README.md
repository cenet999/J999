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

