## 加入定时任务
(crontab -l 2>/dev/null; \
 echo "*/20 * * * * /home/ubuntu/J999/autopull.sh >> /home/ubuntu/J999/autopull.log 2>&1") \
 | sort -u | crontab -

## 2026-04-24 改动记录（目录调整）
- `J9_Admin/API/GameApi` 已整体迁至 `J9_Admin/Services/GameApi`（含 `old/` 与配置/说明文件）；命名空间由 `J9_Admin.API.GameApi` 改为 `J9_Admin.Services.GameApi`。`Program.cs`、`GameService.cs`、`GameBetHistorySyncService.cs`、`_DGame.razor`、`_DTransAction.razor`、单元测试与本文档路径已同步更新。
- `J9_Admin/API/PayApi` 已迁至 `J9_Admin/Services/PayApi`（`Pay0Api.cs`）；命名空间 `J9_Admin.API.PayApi` → `J9_Admin.Services.PayApi`。`Program.cs`、`DTransActionService.cs` 已更新。
- `J9_Admin/Components/Ddd/_DTransAction.razor`：「一键同步注单」改为注入 `GameBetHistorySyncService.SyncMsAndXhForUsernameAsync`（与会员端 / 定时任务同一套 MS+XH 时间窗）；去掉原仅作用于 MS 的起止时间输入，避免与服务内固定窗口不一致。

## 2026-04-24 改动记录
- `J9_Admin/Services/GameBetHistorySyncService.cs`（新增）：封装与会员端一致的 MS（近 7 日）+ XH（北京时间近 24 小时）注单落库；`TransActionService.SyncBetHistoryToDatabaseAsync` 改为调用此服务。
- `J9_Admin/Services/GameBetHistorySyncHostedService.cs`（新增）：后台定时任务，**始终开启**，每 **6 小时**调用一次 `SyncMsAndXhAllAsync`：**登录名留空**（传给 MS/XH 为 `null`），单次拉全站 MS（近 7 日）+ XH（北京时间近 24 小时）并落库（启动后 120 秒首轮，常量写在类内，无需 appsettings）。参照 SmartQC 在 `CreateScope` 内解析 Scoped 服务执行；`Program.cs` 注册 `AddHostedService`。
- `J9_Admin/Entities/Ddd/DTransAction.cs`：`TransactionTime` 由 `DateTime` 改为 **`long` Unix 时间戳（UTC，秒）**，与 XH/MS 注单字段 `betTime` 一致。库表 `ddd_transaction.transaction_time` 在 FreeSql 自动同步下会变为整型/长整型；**已有环境若为 datetime 需自行迁移或重建列**（把旧值转为 Unix 秒再改列类型）。`J9_Admin/Utils/TimeHelper.cs`（原 `GamerecordTimeHelper` 已重命名）统一提供北京时间工具：`BeijingTz`/`BeijingNow`/`BeijingToUnix`、`UnixToBeijing`、本机侧 `LocalToUnix`/`UnixToLocal`、`UtcUnix`（缺省「当前北京时刻 Unix」用 `BeijingToUnix(BeijingNow())`；本机展示用 `UnixToLocal(...).ToString(...)`）；`XHGameApi`/`MSGameApi` 新注单的 `CreatedTime`/`ModifiedTime` 仍用 `UnixToBeijing(TransactionTime)` 与原先语义一致。后台 `_DTransAction.razor`、`_DAgentSettlement.razor` 与相关 API 已按秒比较；`J9_APP_103` 的 `transactions.tsx`、`rebate.tsx` 已兼容接口返回的数字时间戳。
- `J9_Admin/Utils/TimeHelper.cs`：`BeijingTz`、`BeijingNow`、`BeijingToUnix`、`UnixToBeijing`，以及流水 `TransactionTime` 用的 `UtcUnix`、`LocalToUnix`、`UnixToLocal`（原 `TransActionUnixTime` / `GamerecordTimeHelper` 能力保留，类名与方法名已简化）。`XHGameApi` / `MSGameApi` 使用 `using static TimeHelper`；其余服务用 `TimeHelper.*`。
- `J9_Admin/Services/GameApi/MSGameApi.cs`：`ConvertBetRecordToTransAction` 中投注时间与 **XH** 完全一致：缺省 `betTime` 用 `BeijingToUnix(BeijingNow())`、`UnixToBeijing` 解析已有 Unix 秒（`using static TimeHelper`）。
- `J9_Admin/API/DTransActionService.cs` / `J9_Admin/Services/GameApi/MSGameApi.cs` / `GameService.cs`：MS 注单与会员「同步注单」近 7 日窗口统一为**北京时间**：边界用 `TimeHelper.BeijingNow()`，字符串用 `CultureInfo.GetCultureInfo("zh-CN")` 与 `yyyy-MM-dd HH:mm:ss`（不再用 `InvariantCulture` / `DateTime.Now`）。`MSGameApi.GetBetHistory` 以 `zh-CN` 解析 `from`/`to` 后按 `BeijingTz()` 转 Unix 秒写入 `start_at`/`end_at`。`GameService` 拉 MS 历史未传时间时默认同上。会员同步里 XH 段的 `xhFrom`/`xhTo` 亦改为 `zh-CN` 格式化；XH 仍为北京时间近 24 小时单次 `gamerecord`；REST 不传 `username` 时为全商户注单。
- `J9_Admin/Services/GameApi/XHGameApi.cs`：所有 XH RestSharp 请求经 `CreateXhRestClient`，在底层 `SocketsHttpHandler` / `HttpClientHandler` 上显式 `Tls12 | Tls13`，减轻部分环境下访问 `ap.xh-api.com` 时出现「SSL connection could not be established」；注单拉取失败日志会带上 `ErrorException` 便于查看根因（代理/证书等）。

## 2026-04-22 改动记录
- `J9_Admin/Components/Ddd/_DGame.razor`：同步平台下拉框改为从数据库 `DGamePlatform` 动态加载，并按平台名称自动映射到 `MS/XH` 同步逻辑（保留静态兜底项）。
- `J9_Admin/SeedData/MenuSeedData.cs`：同步 API 菜单权限项，补齐 `Game` 下 XH 系列接口按钮，以及 `Login` 下 `ChangeWithdrawPassword`、`GetTenantInfo`。

## 2026-04-23 改动记录：每日任务 & 限时活动 & 平台公告 & 游戏平台 种子数据
- `J9_Admin/SeedData/Ddd/GamePlatformSeedData.cs`（新增）：把 `buyu.db.ddd_game_platform` 中 2 条游戏平台（美盛游戏 / 星汇游戏）固化成种子数据；按 `Name` 判重，可重复执行。需与 `_DGame.razor` 同步逻辑（MS / XH）名称匹配。
- `J9_Admin/SeedData/Ddd/TaskSeedData.cs`（新增）：把 `buyu.db.ddd_task` 中 5 条每日任务（每日登录 / 每日签到 / 每日充值 / 参与游戏 / 邀请好友）固化成种子数据；按 `TaskType + Title` 判重，可重复执行。
- `J9_Admin/SeedData/Ddd/EventSeedData.cs`（新增）：把 `buyu.db.ddd_event` 中 3 条限时活动（周末双倍积分畅玩 / VIP特权升级月 / 首充回馈100%）固化成种子数据；自动关联到第一个代理（`DAgent`）；按 `Title` 判重，可重复执行；若系统内暂无代理则静默跳过，等下一次应用启动再补。
- `J9_Admin/SeedData/Ddd/NoticeSeedData.cs`（新增）：把 `buyu.db.ddd_notice` 中 5 条平台公告（新服盛大开启 / 充值延迟说明 / 周末双倍爆率 / 停服维护 / 外挂声明）固化成种子数据；按 `Title` 判重，可重复执行。
- `J9_Admin/Program.cs`：启动时除 `MenuSeedData.Initialize` 外，追加调用 `GamePlatformSeedData.Initialize` / `TaskSeedData.Initialize` / `EventSeedData.Initialize` / `NoticeSeedData.Initialize`。
- 备注：`J9_Admin/API/LoginService.cs::InitDbData` 中原本的每日任务初始化逻辑予以保留（兼容老接口），新增的种子数据以启动时幂等写入为主。

## 2026-04-23 改动记录：生产环境接入 Supabase PostgreSQL
- `J9_Admin/J9_Admin.csproj`：新增 `FreeSql.Provider.PostgreSQL 3.5.212`。
- `J9_Admin/Program.cs`：数据库连接改为从配置读取，`ConnectionStrings:DataType` + `ConnectionStrings:Default`；无配置时回退到 SQLite（`Data Source=buyu.db`），保持本地调试不变。
- `J9_Admin/appsettings.json`：新增 `ConnectionStrings` 默认配置（Sqlite / `buyu.db`）。
- `J9_Admin/appsettings.Production.json`（新增）：生产环境使用 Supabase PostgreSQL
  - Host：`db.ogxaxfwsdafckmrfrlzs.supabase.co`，Database：`postgres`，Username：`postgres`
  - 密码占位：`[YOUR-PASSWORD]`，请在部署时替换，或用环境变量覆盖：
    `ConnectionStrings__Default="Host=...;Port=5432;Database=postgres;Username=postgres;Password=xxx;SSL Mode=Require;Trust Server Certificate=true"`
  - Dockerfile 已设置 `ASPNETCORE_ENVIRONMENT=Production`，容器启动会自动加载 `appsettings.Production.json`。
- 注意：FreeSql `UseAutoSyncStructure(true)` 开启了自动建表；首次连到空库会自动创建实体表结构；线上已有库建议提前备份。

