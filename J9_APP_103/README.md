# J9_APP_103

这是九游俱乐部的 Expo 移动端项目，支持 Android、iOS 和 Web。

## 先安装依赖

```bash
cd J9_APP_103
pnpm install
```

## 本地开发运行

启动 Expo 开发服务：

```bash
pnpm dev
```

常用运行命令：

```bash
pnpm android
pnpm ios
pnpm web
```

说明：

- `pnpm dev`：只启动开发服务
- `pnpm android`：启动开发服务，并尝试打开 Android 模拟器
- `pnpm ios`：启动开发服务，并尝试打开 iOS 模拟器
- `pnpm web`：以网页方式运行

## 真机联调

如果你要让手机访问本机服务，可以使用：

```bash
pnpm dev:server
```

这个命令会用 tunnel 模式启动，端口是 `8099`。

如果后端不在本机，需要先设置 `.env`，例如：

```env
EXPO_PUBLIC_API_URL=http://你的服务器IP:8015
```

## Android 打包

注意：

- 安装包里的接口地址不是看你手机当前网络自动判断的，而是打包时写进去的。
- 开发环境可以从根目录 `.env` 读取 `EXPO_PUBLIC_API_URL`；正式包接口地址写在 `lib/api/request.ts`。
- `eas.json` 这里只负责区分构建模式，接口地址不一定非要写在里面。
- 如果后端地址变了，改完 `.env` 后需要重新打包，旧安装包不会自动更新。

### 1. 测试安装包

 ```bash
 pnpm build:android
 ```

这个命令走的是 `production` 配置，生成的是 **apk**，适合自己安装测试或直接发给别人安装。

如果你还想走原来的测试配置，也可以用：

```bash
pnpm build:android:preview
```

这个命令走的是 `preview` 配置，生成的也是 **apk**。

### 2. 生产发布包

```bash
pnpm build:android:aab
```

这个命令走的是 `store` 配置，生成的是 **aab**，适合上架应用商店。

## iOS 打包

```bash
pnpm build:ios
```

这个命令会走生产配置，用于 iOS 发布构建。

## 其他常用命令

类型检查：

```bash
npx tsc --noEmit
```

打包 Web：

```bash
pnpm build:web
```

清理后重装：

```bash
pnpm clean
pnpm install
```

## 一句话记住

- 日常开发：`pnpm dev`
- 安卓模拟器运行：`pnpm android`
- 安卓测试包：`pnpm build:android`，产物是 `apk`
- 安卓备用测试包：`pnpm build:android:preview`，产物是 `apk`
- 安卓商店包：`pnpm build:android:aab`，产物是 `aab`
