---
name: aspnet-api-operation-logging
description: >
  为任意 ASP.NET Core MVC/Web API 项目增加「入站请求 + 出站结果 + 异常」结构化运维日志：TraceId、
  HTTP 方法与路径、控制器/动作名、ActionArguments 序列化与敏感字段脱敏、Stopwatch 耗时、
  IActionResult 类型化摘要。典型接法为 AddScoped + 在 API 控制器基类上使用 ServiceFilter，或与
  AddControllers 全局筛选器二选一（见正文取舍）。复制 Filters 下的单一筛选器类即可移植到新解决方案。
---

# ASP.NET API 运维日志（IAsyncActionFilter）

## 在新项目中落地

1. **新增文件**  
   在目标项目中创建 `Filters/ApiOperationLoggingFilter.cs`（类名可改，下文以该名为例）。命名空间改为当前项目的根命名空间或约定的 `*.Filters`。

2. **注册 DI**  
   `Program.cs`（或 `Startup.cs`）：
   `builder.Services.AddScoped<{命名空间}.Filters.ApiOperationLoggingFilter>();`  
   使用 `AddScoped` 以便注入 `ILogger<ApiOperationLoggingFilter>`，并与「按请求解析」的控制器筛选器生命周期一致。

3. **启用方式（二选一）**

   - **基类 + ServiceFilter（推荐，多项目一致）**  
     在公共 API 基类（如 `ApiControllerBase`）上标注：  
     `[ServiceFilter(typeof(ApiOperationLoggingFilter))]`  
     所有继承该基类的控制器自动记录；便于与同一基类上的 `[Authorize]`、`[ApiController]` 等并列管理。

   - **全局注册**  
     `builder.Services.AddControllers(options => { options.Filters.Add<ApiOperationLoggingFilter>(); });`  
     并仍将筛选器类型注册为 `AddScoped<ApiOperationLoggingFilter>()`（全局过滤器从 DI 解析时需要）。  
     适用：没有统一 API 基类、或希望所有控制器（含非 API）都记录。注意全局过滤器**无法**用 `ServiceFilter` 那种「只对部分控制器」的粒度，需配合 `IAsyncActionFilter` 内判断路由或特性再短路。

4. **与授权筛选器的顺序**  
   若要在**未授权**请求仍打入站日志，实现 `IOrderedFilter`，将 `Order` 设为比授权过滤器**更靠前**（更先执行）的数值（例如授权用默认 0，则日志可用 `-2000`）。以你项目中实际授权方式的 `Order` 为准。

## 筛选器实现要点（与实现语言无关的契约）

- **入站**：`Activity.Current?.Id ?? HttpContext.TraceIdentifier` 作为 TraceId；`ControllerActionDescriptor` 取 `ControllerName`/`ActionName`，否则从 `RouteData.Values` 取 `controller`/`action`；将 `ActionArguments` 格式化为单行 JSON。
- **脱敏**：`HashSet<string>(StringComparer.OrdinalIgnoreCase)` 维护敏感属性名；顶层参数名命中写 `***`；对象序列化为 JSON 后递归处理子对象/数组元素。按业务扩充（如 `password`、`token`、`refreshToken` 等）。
- **特殊类型**：`IFormFile` 只记录文件名与 `Length`，不读取流。
- **一般对象**：序列化时使用忽略循环引用与最大深度限制；序列化失败则回退 `ToString()`。
- **执行**：`Stopwatch` 包裹 `await next()`；管道外抛错在 `catch` 记 Error 后重新抛出；`ActionExecutedContext.Exception` 未处理时记 Error，否则记出站 Information。
- **出站摘要**：处理 `JsonResult`、`ObjectResult` 的 `Value`；若返回值类型含 `Success` 属性可输出简短业务摘要（如 Success/Code/Message）；否则 JSON 序列化并限制长度；其它结果类型用 `switch` 给出短描述（状态码、文件、重定向等）。

## 日志消息格式（建议固定前缀便于检索）

- `[API入站]` — TraceId、Method、Path、Controller.Action、Args=
- `[API异常]` — 同上 + 耗时
- `[API出站]` — 同上 + Result=

## 依赖

- 一种常见实现使用 **Newtonsoft.Json**（`JObject`、脱敏递归）。若目标项目已统一 **System.Text.Json**，则改用 `JsonNode` 或自定义访问器实现同等脱敏与深度限制，不必新增 Newtonsoft 依赖。

## 本仓库中的示例（可选对照）

单仓内完整示例见：`J9_Admin/Filters/ApiOperationLoggingFilter.cs`；接入示例：`Program.cs` 中 `AddScoped`，`J9_Admin/API/BaseService.cs` 中 `[ServiceFilter]`。复制到其他项目时请删除 J9 专用命名空间并替换为你的命名空间。

## Skill 自检

```bash
python3 {path-to-skill-creator}/scripts/quick_validate.py {path-to-this-skill-folder}
```

将 `{path-to-skill-creator}`、`{path-to-this-skill-folder}` 换为本机路径。
