# J9_NeoAdmin 开发上手指南

> 九游俱乐部（J9 Club）后台宿主项目。基于 **NoAdmin.Blazor** + **BootstrapBlazor** + **FreeSql**，为 **J9_APP_103** 移动端提供 REST API 与管理端 UI。
>
> 本文档描述 **J9_NeoAdmin** 目录下的实际工程；框架通用能力详见 monorepo 内 [`../NoAdmin/README.md`](../NoAdmin/README.md) 与 [`../NoAdmin/docs/tutorials/`](../NoAdmin/docs/tutorials/)。

---

## 目录

1. [在 Monorepo 中的位置](#1-在-monorepo-中的位置)
2. [架构与目录结构](#2-架构与目录结构)
3. [快速开始](#3-快速开始)
4. [Program.cs 与配置](#4-programcs-与配置)
5. [实体与数据访问](#5-实体与数据访问)
6. [Blazor 管理页面](#6-blazor-管理页面)
7. [菜单种子 SeedData](#7-菜单种子-seeddata)
8. [移动端 REST API](#8-移动端-rest-api)
9. [定时任务与后台服务](#9-定时任务与后台服务)
10. [与 J9_APP_103 联调](#10-与-j9_app_103-联调)
11. [Docker 部署](#11-docker-部署)
12. [典型开发流程](#12-典型开发流程)
13. [常见坑与最佳实践](#13-常见坑与最佳实践)
14. [框架文档索引](#14-框架文档索引)

---

## 1. 在 Monorepo 中的位置

```
J999/                          # Monorepo 根目录
├── J9_NeoAdmin/               # ← 本指南（NoAdmin 版后台，迁移目标）
├── J9_Admin/                  # 旧版 AdminBlazor 后台（并行存在）
├── J9_APP_103/                # React Native 移动端
├── NoAdmin/                   # NoAdmin.Blazor 框架源码
│   └── NoAdmin.Blazor/
└── AGENTS.md                  # 仓库级命令与架构说明
```

| 项 | 值 |
|----|-----|
| 目录名 | `J9_NeoAdmin` |
| 程序集 / 命名空间 | `J9_NeoAdmin` |
| 框架引用 | `ProjectReference` → `../NoAdmin/NoAdmin.Blazor/` |
| 解决方案 | `J9_NeoAdmin/J9_NeoAdmin.sln` |

**注意**：本文档来自 NeoAdmin 模板项目的移植版，已按 J9 实际技术栈改写。框架 API 名称是 **NovaAdmin** / **NoAdmin**，不是 NeoAdmin / NeoUI。

---

## 2. 架构与目录结构

### 2.1 三层关系

```
┌─────────────────────────────────────────────────────────────┐
│  J9_NeoAdmin（宿主 / J9 业务）                                │
│  · Program.cs、API/、Services/、TelegramBot/                 │
│  · Entities/Ddd/   J9 核心业务实体                           │
│  · Components/Ddd/ J9 管理页面                               │
│  · SeedData/       菜单与演示数据                            │
└──────────────────────────┬──────────────────────────────────┘
                           │ ProjectReference
┌──────────────────────────▼──────────────────────────────────┐
│  NoAdmin.Blazor（框架，../NoAdmin/NoAdmin.Blazor/）           │
│  · NovaAdminTable、LayoutAdmin、NovaSelect*                   │
│  · 系统管理 /admin/*、登录、权限、定时任务基础设施              │
└──────────────────────────┬──────────────────────────────────┘
                           │ NuGet 依赖
┌──────────────────────────▼──────────────────────────────────┐
│  BootstrapBlazor（UI 组件库）                                 │
│  · Switch、DateTimePicker、Table、Toast 等                    │
│  · 文档：https://www.blazor.zone/                            │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 宿主目录树

```
J9_NeoAdmin/
├── Program.cs
├── J9_NeoAdmin.csproj
├── appsettings.json
├── GlobalUsings.cs
├── API/                    # 移动端 REST 服务（LoginService、GameService 等）
│   └── DTOs/
├── Components/
│   ├── App.razor / Routes.razor / _Imports.razor
│   ├── Pages/              # Home、Admin 入口
│   ├── Ddd/                # J9 业务 CRUD 页（_DMember.razor 等）
│   └── Blog/               # 框架博客演示页
├── Entities/
│   ├── Ddd/                # DMember、DGame、DAgent、DTransAction …
│   └── Blog/               # 博客演示实体
├── SeedData/
│   ├── MenuSeedData.cs
│   └── Ddd/                # GamePlatform、Event、Task、Notice 种子
├── Services/               # 游戏 API、支付、注单同步、代理结算
├── TelegramBot/            # 生产环境 Telegram Bot
├── Utils/
├── wwwroot/
├── dotnet.sh               # dotnet watch run
├── docker-compose.yaml
└── Dockerfile
```

### 2.3 与旧 J9_Admin / SmartQC 的对应

| 旧概念 | J9_NeoAdmin 对应 |
|--------|------------------|
| `AdminBlazor.dll`（反编译参考） | `NoAdmin.Blazor` 源码（`../NoAdmin/`） |
| `J9_Admin/Component/` | `J9_NeoAdmin/Components/` |
| `Entities/Ddd/` | 保持不变 |
| `SeedData/` | 保持不变，`MenuSeedData.Initialize` 模式 |
| `NovaSelect*` / `NovaAdminTable` | 同名，在 NoAdmin.Blazor 中 |
| NeoAdmin 的 `CrudTable` / `NeoSelect*` / `NeoUI` | **本项目不使用** |

**约定：扩展业务时，保持 `Entities/`、`Components/`、`SeedData/` 三处同步。**

---

## 3. 快速开始

### 3.1 环境要求

| 依赖 | 说明 |
|------|------|
| .NET 8 SDK | `TargetFramework: net8.0` |
| Node.js | 仅 BootstrapBlazor 资源，无 Tailwind 编译步骤 |

### 3.2 本地运行

```bash
cd J9_NeoAdmin
dotnet restore
dotnet run
# 或热重载
./dotnet.sh
```

| 项 | 值 |
|----|-----|
| 开发地址 | http://localhost:5231 |
| Swagger | http://localhost:5231/api |
| 版本接口 | http://localhost:5231/profile |
| 默认账号 | admin / admin |

### 3.3 编译整个解决方案

```bash
dotnet build J9_NeoAdmin/J9_NeoAdmin.sln
# 或从仓库根目录
dotnet build J999.sln
```

---

## 4. Program.cs 与配置

### 4.1 启动流程摘要

```csharp
// 1. Serilog、CORS、DataProtection
// 2. 读取 ConnectionStrings（支持 ActiveProvider 切换 Sqlite / PostgreSQL）
builder.AddNovaAdmin(new NovaAdminOptionsItem
{
    Assemblies = [typeof(Program).Assembly],
    FreeSqlBuilder = a => a
        .UseConnectionString(dbType, dbConnStr)
        .UseAutoSyncStructure(true),
    SchedulerExecuting = OnSchedulerExecuting
});

// 3. 注册 J9 业务服务（GameService、LoginService、Telegram 等）
// 4. Razor Components + BootstrapBlazor
app.UseCors("CorsPolicy");
app.UseBootstrapBlazor();
app.MapRazorComponents<J9_NeoAdmin.Components.App>()
    .AddAdditionalAssemblies(typeof(NovaAdminOptionsItem).Assembly)
    .AddInteractiveServerRenderMode();

app.MapGet("/profile", () => new { app = "J9_NeoAdmin", ... });
app.UseAdminOmniApi();

// 5. 种子数据初始化
MenuSeedData.Initialize(fsql);
// GamePlatformSeedData、TaskSeedData …
```

### 4.2 appsettings 要点

```json
{
  "ConnectionStrings": {
    "ActiveProvider": "PostgreSQL",
    "Sqlite": { "DataType": "Sqlite", "Default": "Data Source=buyu.db" },
    "PostgreSQL": { "DataType": "PostgreSQL", "Default": "..." }
  },
  "AllowedOrigins": ["http://localhost:5231", "http://localhost:8081"],
  "TelegramBot": { "ApiKey": "...", "StartupNotifyChatIds": "..." },
  "APIDomain": "https://..."
}
```

| 配置项 | 说明 |
|--------|------|
| `ConnectionStrings:ActiveProvider` | 切换 `Sqlite` / `PostgreSQL` |
| `AllowedOrigins` | 移动端与 Web 跨域白名单 |
| `TelegramBot` | 仅非 Development 环境启动 Bot |
| `APIDomain` | 对外 API 域名 |

### 4.3 _Imports.razor

已全局引用 `NoAdmin.Blazor`、`BootstrapBlazor.Components`、`FreeSql` 等，新业务页一般无需重复 using。

---

## 5. 实体与数据访问

### 5.1 基类继承链

```
Entity → EntityCreated → EntityModified → EntityAudited
```

| 基类 | J9 场景 |
|------|---------|
| `EntityModified` | 绝大多数 Ddd 实体（`DMember`、`DGame` …） |
| `EntityAudited` | 需审批流（见 Blog 演示，J9 核心业务暂未使用） |

### 5.2 实体示例

```csharp
using FreeSql.DataAnnotations;

namespace J9_NeoAdmin.Entities.Ddd;

[Table(Name = "d_member")]
public class DMember : EntityModified
{
    [Column(StringLength = 50)]
    public string Username { get; set; } = string.Empty;

    public decimal CreditAmount { get; set; }
    // 导航属性
    public DAgent? DAgent { get; set; }
}
```

### 5.3 FreeSql 用法

```csharp
// API 服务（BaseService._fsql）
var members = await _fsql.Select<DMember>()
    .Include(m => m.DAgent)
    .Where(m => m.IsEnabled)
    .OrderByDescending(m => m.Id)
    .ToListAsync();
```

表结构由 `UseAutoSyncStructure(true)` 自动同步，无需手写 `SyncStructure`（与 NeoAdmin 模板不同）。

---

## 6. Blazor 管理页面

### 6.1 NovaAdminTable 页面模板

参考 `Components/Ddd/_DMember.razor`：

```razor
@page "/Ddd/DMember"

<PageTitle>玩家管理</PageTitle>

<NovaAdminTable TItem="DMember" Context="item" PageSize="50" Title="玩家管理"
    DialogClassName="modal-xl" InitQuery="InitQuery" OnQuery="OnQuery" OnEdit="OnEdit">
    <TableHeader>
        <th>用户名</th>
        <th>余额</th>
    </TableHeader>
    <TableRow>
        <td>@item.Username</td>
        <td>@item.CreditAmount.ToString("N2")</td>
    </TableRow>
    <EditTemplate>
        <div class="form-group col-4">
            <label class="form-label">用户名</label>
            <input @bind="item.Username" class="form-control">
        </div>
    </EditTemplate>
</NovaAdminTable>

@code {
    async Task InitQuery(NovaAdminQueryInfo e) { /* 配置筛选器 */ }
    void OnQuery(NovaAdminQueryEventArgs<DMember> e) =>
        e.Select.Include(a => a.DAgent).OrderByDescending(a => a.Id);
}
```

### 6.2 与 NeoAdmin CrudTable 的差异

| NeoAdmin（旧文档） | J9_NeoAdmin（实际） |
|-------------------|---------------------|
| `CrudTable` | `NovaAdminTable` |
| `CrudQueryInfo` / `CrudQueryEventArgs` | `NovaAdminQueryInfo` / `NovaAdminQueryEventArgs` |
| `DataTableColumn` + Tailwind | `TableHeader` + `TableRow` + Bootstrap 栅格 |
| `NeoSelectDict` | `NovaSelectDict` |
| `Label` + `Input`（NeoUI） | `form-label` + `form-control` / BootstrapBlazor 组件 |

### 6.3 框架组件

| 场景 | 组件 |
|------|------|
| 标准 CRUD | `NovaAdminTable` |
| 字典 | `NovaSelectDict` |
| 枚举 | `NovaSelectEnum<T>` |
| 实体选择 | `NovaSelectEntity<TItem,TKey>` |
| 列排序 | `NovaAdminSort` |

详细参数见 [`../NoAdmin/docs/tutorials/01-novaadmintable.md`](../NoAdmin/docs/tutorials/01-novaadmintable.md)。

### 6.4 审批流（可选）

实体继承 `EntityAudited`，表格设 `IsAudit="true"`。参考 `NoAdmin` 模板中的 `_AuditDemo.razor`。

---

## 7. 菜单种子 SeedData

### 7.1 初始化入口

`Program.cs` 启动时调用：

```csharp
J9_NeoAdmin.SeedData.MenuSeedData.Initialize(fsql);
J9_NeoAdmin.SeedData.Ddd.GamePlatformSeedData.Initialize(fsql);
// TaskSeedData、EventSeedData、NoticeSeedData …
```

### 7.2 新增菜单

在 `SeedData/MenuSeedData.cs` 对应 `CreateXxxMenu()` 方法中增加子菜单：

- `Label`：侧边栏显示名
- `Path`：必须与 `@page` 路由一致（如 `/Ddd/DMember`）
- `Type`：增删改查类型自动附带 add/edit/remove 按钮权限

`Initialize` 会递归检查：不存在则插入，已存在则补齐子节点。

---

## 8. 移动端 REST API

### 8.1 暴露方式

`app.UseAdminOmniApi()` 自动扫描并注册 `API/` 下带 `[ApiController]` 的服务类。

### 8.2 服务类模板

```csharp
[ApiController]
[Route("api/login")]
[Tags("会员系统")]
public class LoginService : BaseService
{
    [HttpPost($"@{nameof(Register)}")]
    [AllowAnonymous]
    public async Task<ApiResult> Register([FromBody] RegisterRequest request)
    {
        // 手动验证 + _fsql 业务逻辑
    }
}
```

- 路由风格：`/api/login/@Register`
- 返回类型：`ApiResult` / `ApiResult<T>`
- 基类 `BaseService` 提供 `_fsql`、`_scheduler`、`_adminContext`、`_logger` 等

### 8.3 主要 API 模块

| 服务类 | 路由前缀 | 用途 |
|--------|----------|------|
| `LoginService` | `api/login` | 注册、登录、改密、个人中心 |
| `GameService` | `api/game` | 游戏列表、进入游戏 |
| `DTransActionService` | `api/transaction` | 充提记录 |
| `MessageService` | `api/message` | 站内消息 |
| `NoticeService` | `api/notice` | 公告 |
| `EventService` | `api/event` | 活动 |
| `BannerService` | `api/banner` | 轮播图 |
| `TaskProgressService` | `api/task` | 任务进度 |

---

## 9. 定时任务与后台服务

### 9.1 OnSchedulerExecuting

`Program.cs` 中按 `task.Topic` 分发：

```csharp
static void OnSchedulerExecuting(IServiceProvider service, TaskInfo task)
{
    switch (task.Topic)
    {
        // case "your.task.id": ...
    }
}
```

### 9.2 HostedService

| 服务 | 说明 |
|------|------|
| `GameBetHistorySyncHostedService` | 注单历史同步 |
| `TelegramBotService` | 生产环境 Telegram Bot（Development 跳过） |

### 9.3 业务 Scoped 服务

`AgentWeeklySettlementService`、`GameBetHistorySyncService`、各 `GameApi` / `PayApi` 实现在 `Services/` 目录。

管理界面：`/admin/task-scheduler`

---

## 10. 与 J9_APP_103 联调

移动端默认连接 `http://localhost:5231`，可通过环境变量覆盖：

```bash
# J9_APP_103/.env
EXPO_PUBLIC_API_URL=http://<服务器IP>:8015
```

```bash
cd J9_APP_103
pnpm install
pnpm dev          # 本地 Expo
pnpm dev:server   # 隧道模式，端口 8099
```

确保 `appsettings.json` 的 `AllowedOrigins` 包含移动端来源地址。

---

## 11. Docker 部署

```bash
cd J9_NeoAdmin
./docker-auto.sh    # build + down + up -d
# 或
docker compose up
```

| 项 | 值 |
|----|-----|
| 容器名 | `j9-admin-backend` |
| 宿主机端口 | `8015` → 容器 80 |
| 数据卷 | `Logs`、`wwwroot/uploads`、`keys`、`buyu.db` |

---

## 12. 典型开发流程

以新增「优惠券」管理为例：

1. **实体** — `Entities/Ddd/DCoupon.cs`，继承 `EntityModified`
2. **页面** — `Components/Ddd/_DCoupon.razor`，`@page "/Ddd/DCoupon"` + `NovaAdminTable`
3. **菜单** — `SeedData/MenuSeedData.cs` 对应菜单组增加 Path `/Ddd/DCoupon`
4. **（可选）API** — `API/CouponService.cs` 供移动端调用
5. **验证** — `dotnet run`，检查管理页与 `/api` Swagger

---

## 13. 常见坑与最佳实践

| 问题 | 原因 / 解决 |
|------|-------------|
| 框架 `/admin/*` 404 | 缺少 `AddAdditionalAssemblies(typeof(NovaAdminOptionsItem).Assembly)` |
| 新菜单不显示 | `MenuSeedData.Initialize` 未执行或角色未分配菜单 |
| API POST 报验证异常 | 已禁用 ModelState 自动验证，须在 Service 内手动校验 |
| 移动端 401 | Token 过期；检查 `LoginService` 与 `request.ts` 鉴权逻辑 |
| Telegram 本地抢线上 Bot | Development 环境已默认跳过 Bot 启动 |
| 误提交数据库 | `buyu.db` 在仓库中，避免提交本地测试数据 |
| 照搬 NeoAdmin 文档 | 组件名、UI 库、启动 API 均不同，以本文档为准 |

---

## 14. 框架文档索引

| 资源 | 路径 |
|------|------|
| NoAdmin 总览 | [`../NoAdmin/README.md`](../NoAdmin/README.md) |
| NovaAdminTable 教程 | [`../NoAdmin/docs/tutorials/01-novaadmintable.md`](../NoAdmin/docs/tutorials/01-novaadmintable.md) |
| NovaInputTable | [`../NoAdmin/docs/tutorials/03-novainputtable.md`](../NoAdmin/docs/tutorials/03-novainputtable.md) |
| NovaButton | [`../NoAdmin/docs/tutorials/07-novabutton.md`](../NoAdmin/docs/tutorials/07-novabutton.md) |
| 仓库 AGENTS.md | [`../AGENTS.md`](../AGENTS.md) |
| Cursor 规则 | [`./.cursor/rules/`](./.cursor/rules/) |

---

*最后更新：适配 J9_NeoAdmin + NoAdmin.Blazor 实际工程（自 NeoAdmin 模板文档移植并改写）。*
