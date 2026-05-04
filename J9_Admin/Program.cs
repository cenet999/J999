using FreeScheduler;
using FreeSql;
using J9_Admin.API;
using J9_Admin.Services;
using J9_Admin.TelegramBot;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Serilog;
using J9_Admin.Services.DatabaseSync;


// 配置 Serilog - 支持环境特定配置
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateLogger();

try
{
    Log.Information("启动应用程序...");
    Log.Information($"当前环境: {environment}");

    if (args.Length > 0 && string.Equals(args[0], "sync-pg-to-sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // 同步专用：PostgreSQL 等连接串只读 appsettings.json，避免 Development 里占位串覆盖真实密码。
        // 仍支持环境变量覆盖（如 ConnectionStrings__PostgreSQL__Default）。
        var syncConfiguration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        await PostgreSqlToSqliteSyncRunner.RunAsync(syncConfiguration, environment);
        return;
    }

    var builder = WebApplication.CreateBuilder(args);


    // 验证配置是否正确加载
    var domain = builder.Configuration["APIDomain"];
    Log.Information($"API域名配置: {domain}");

    // 添加数据保护配置
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("keys")) //替换为你的实际路径
        .SetApplicationName("gougouchacha_bot");

    // 添加 Serilog 到服务容器
    builder.Host.UseSerilog();

    // 添加CORS服务
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", corsBuilder =>
        {
            corsBuilder.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins))
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials(); // 必须开启以支持携带 Token 等身份凭证
        });
    });


    // 数据库连接：优先读取新的多数据库配置 ConnectionStrings:ActiveProvider
    // 若未配置，则回退到旧格式 ConnectionStrings:Default / ConnectionStrings:DataType
    var activeDbProvider = builder.Configuration["ConnectionStrings:ActiveProvider"];
    var dbSection = !string.IsNullOrWhiteSpace(activeDbProvider)
        ? builder.Configuration.GetSection($"ConnectionStrings:{activeDbProvider}")
        : builder.Configuration.GetSection("ConnectionStrings");
    var dbTypeText = dbSection["DataType"] ?? builder.Configuration["ConnectionStrings:DataType"];
    var dbConnStr = dbSection["Default"] ?? builder.Configuration.GetConnectionString("Default");
    DataType dbType;
    if (!string.IsNullOrWhiteSpace(dbTypeText) && Enum.TryParse<DataType>(dbTypeText, true, out var parsedType))
    {
        dbType = parsedType;
    }
    else
    {
        dbType = DataType.Sqlite;
    }
    if (string.IsNullOrWhiteSpace(dbConnStr))
    {
        dbConnStr = "Data Source=buyu.db";
        dbType = DataType.Sqlite;
    }
    Log.Information("数据库配置节点: {DbProvider}", string.IsNullOrWhiteSpace(activeDbProvider) ? "ConnectionStrings(Default)" : activeDbProvider);
    Log.Information("数据库类型: {DbType}", dbType);

    // 生产环境 PostgreSQL 不自动改表：AdminBlazor 内置 SysUserLoginLog.Ip 仍是 VARCHAR(50)，
    // AutoSyncStructure 会尝试把已放宽的字段收窄，遇到历史长 IP 数据会导致启动失败。
    var shouldAutoSyncStructure = !(dbType == DataType.PostgreSQL && builder.Environment.IsProduction());
    Log.Information("FreeSql AutoSyncStructure: {AutoSyncStructure}", shouldAutoSyncStructure);

    // PostgreSQL：在 AdminBlazor 注册 FreeSql（含 AutoSyncStructure）之前放宽 SysUserLoginLog.Ip，避免历史列长与实体不一致阻塞启动。
    using (var startupFs = new FreeSqlBuilder().UseConnectionString(dbType, dbConnStr).Build())
    {
        EnsureSysUserLoginLogIpLength(startupFs, dbType);
    }

    builder.AddAdminBlazor(new AdminBlazorOptions
    {
        Assemblies = [typeof(Program).Assembly],
        FreeSqlBuilder = a => a
            .UseConnectionString(dbType, dbConnStr)
            .UseMonitorCommand(cmd =>
            {
                var lowerCmd = cmd.CommandText.ToLower();
                if (!lowerCmd.Contains("select"))
                {
                    System.Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {cmd.CommandText}\r\n");
                }
            }) //监听SQL语句
            .UseAutoSyncStructure(shouldAutoSyncStructure), // 生产环境 PostgreSQL 禁止自动改表，避免历史字段类型差异阻塞线上编辑。
        SchedulerExecuting = OnSchedulerExecuting //定时任务-自定义触发
    });


    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // 添加目录浏览服务
    builder.Services.AddDirectoryBrowser();

    // Telegram：仅在非开发环境启动 Bot，本地调试时不启用，避免与线上抢占 getUpdates
    // （Token 见 appsettings / 环境变量 TelegramBot__ApiKey）
    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHostedService<J9_Admin.TelegramBot.TelegramBotService>();
        Log.Information("TelegramBot 已启用（当前环境：{Environment}）", builder.Environment.EnvironmentName);
    }
    else
    {
        Log.Information("本地调试环境，已跳过 TelegramBot 启动");
    }
    builder.Services.AddSingleton<J9_Admin.TelegramBot.MessageHandler>();
    builder.Services.AddSingleton<J9_Admin.API.TGMessageApi>();

    // 注册内存缓存服务，供业务服务使用（如ActivityService缓存活动列表）
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<J9_Admin.Utils.PerMemberAsyncGate>();

    // // 注册其他业务服务
    builder.Services.AddScoped<GameBetHistorySyncService>();
    builder.Services.AddScoped<GameIconLocalizationService>();
    builder.Services.AddScoped<AgentWeeklySettlementService>();
    builder.Services.AddHostedService<GameBetHistorySyncHostedService>();

    builder.Services.AddScoped<J9_Admin.API.TransActionService>();
    builder.Services.AddScoped<J9_Admin.API.GameService>();
    builder.Services.AddScoped<J9_Admin.API.LoginService>();
    builder.Services.AddScoped<J9_Admin.Services.GameApi.BuYuGameApi>();
    builder.Services.AddScoped<J9_Admin.Services.GameApi.PgGameApi>();
    builder.Services.AddScoped<J9_Admin.Services.GameApi.MSGameApi>();
    builder.Services.AddScoped<J9_Admin.Services.GameApi.XHGameApi>();
    builder.Services.AddScoped<J9_Admin.API.MessageService>();
    builder.Services.AddScoped<J9_Admin.API.TaskProgressService>();
    builder.Services.AddScoped<J9_Admin.Services.PayApi.Pay0Api>();
    // 注册SessionAgent服务
    builder.Services.AddScoped<J9_Admin.Utils.SessionAgent>();

    // 禁用自动模型验证，必须加上，否则前端提交表单会报异常。无法post数据
    builder.Services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });

    // 启用响应缓存
    builder.Services.AddResponseCaching();

    var app = builder.Build();

    // 使用正确的CORS策略名称
    app.UseCors("CorsPolicy");

    // 配置静态文件服务
    app.UseStaticFiles();

    // IP白名单只限制后台页面访问，不能拦截 App/Web 的公开接口。
    if (environment == "Production")
    {
        app.UseWhen(
            context => ShouldApplyIpWhitelist(context.Request.Path),
            branch => branch.UseMiddleware<J9_Admin.Middlewares.IpWhitelistMiddleware>());
    }

    app.UseAntiforgery();

    app.UseBootstrapBlazor();
    app.MapRazorComponents<J9_Admin.Components.App>()
        .AddAdditionalAssemblies(typeof(AdminBlazorOptions).Assembly)
        .AddInteractiveServerRenderMode();

    // 版本信息接口（必须在 UseAdminOmniApi 之前注册，否则会被框架覆盖）
    app.MapGet("/profile", () => new
    {
        app = "J9_Admin",
        version = "v1.0.2",
        buildTime = File.GetLastWriteTime(typeof(Program).Assembly.Location).ToString("yyyy-MM-dd HH:mm:ss"),
        serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        environment
    });

    app.UseAdminOmniApi();

    // 启用响应缓存中间件
    app.UseResponseCaching();

    #region 初始化种子数据
    var fsql = app.Services.GetService<FreeSqlCloud>();
    if (fsql != null)
    {
        J9_Admin.SeedData.MenuSeedData.Initialize(fsql);
        J9_Admin.SeedData.Ddd.GamePlatformSeedData.Initialize(fsql);
        J9_Admin.SeedData.Ddd.TaskSeedData.Initialize(fsql);
        J9_Admin.SeedData.Ddd.EventSeedData.Initialize(fsql);
        J9_Admin.SeedData.Ddd.NoticeSeedData.Initialize(fsql);
    }
    #endregion

    // 网站启动完成后，可选向指定 Telegram 会话发送成功提示（配置 TelegramBot:StartupNotifyChatIds，逗号分隔）
    MessageHandler.RegisterWebsiteInitializedTelegramNotification(app, environment);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// 自定义触发（顶层局部函数，供上方 AddAdminBlazor 的 SchedulerExecuting 委托引用）
static void OnSchedulerExecuting(IServiceProvider service, TaskInfo task)
{
    switch (task.Topic)
    {
        case "武林大会":
            //todo..
            break;
        case "攻城活动":
            //todo..
            break;
    }
}

static bool ShouldApplyIpWhitelist(PathString path)
{
    if (!path.HasValue)
    {
        return true;
    }

    if (IsPublicAssetPath(path))
    {
        return false;
    }

    return !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/profile", StringComparison.OrdinalIgnoreCase);
}

static void EnsureSysUserLoginLogIpLength(IFreeSql fsql, DataType dbType)
{
    switch (dbType)
    {
        case DataType.Sqlite:
            // SQLite 不按 VARCHAR(n) 强制限制文本长度，无需修改列定义。
            Log.Information("SQLite 数据库无需调整 SysUserLoginLog.Ip 字段长度");
            break;
        case DataType.PostgreSQL:
            fsql.Ado.ExecuteNonQuery(@"
DO $$
DECLARE
    target_table_name text;
    target_column_name text;
BEGIN
    SELECT c.table_name, c.column_name
      INTO target_table_name, target_column_name
      FROM information_schema.columns c
     WHERE c.table_schema = current_schema()
       AND lower(c.table_name) = lower('SysUserLoginLog')
       AND lower(c.column_name) = lower('Ip')
       AND (c.character_maximum_length IS NULL OR c.character_maximum_length < 500)
     LIMIT 1;

    IF target_table_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE VARCHAR(500)', target_table_name, target_column_name);
    END IF;
END $$;");
            Log.Information("已检查 PostgreSQL SysUserLoginLog.Ip 字段长度");
            break;
        default:
            Log.Warning("当前数据库类型 {DbType} 未配置 SysUserLoginLog.Ip 字段长度调整逻辑", dbType);
            break;
    }
}

static bool IsPublicAssetPath(PathString path)
{
    if (path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/avatars", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/game", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    var value = path.Value ?? "";
    return value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase);
}

static bool IsAllowedOrigin(string origin, string[] allowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    var normalizedOrigin = originUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

    foreach (var allowedOrigin in allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(allowedOrigin))
        {
            continue;
        }

        var rule = allowedOrigin.Trim().TrimEnd('/');
        if (!rule.Contains("*."))
        {
            if (string.Equals(normalizedOrigin, rule, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            continue;
        }

        if (IsWildcardOriginMatch(originUri, rule))
        {
            return true;
        }
    }

    return false;
}

static bool IsWildcardOriginMatch(Uri originUri, string rule)
{
    var wildcardMarkerIndex = rule.IndexOf("*.", StringComparison.Ordinal);
    if (wildcardMarkerIndex < 0)
    {
        return false;
    }

    string? scheme = null;
    string hostRule;
    int? port = null;

    if (wildcardMarkerIndex > 0)
    {
        var parserRule = rule.Replace("*.", "wildcard.");
        if (!Uri.TryCreate(parserRule, UriKind.Absolute, out var ruleUri))
        {
            return false;
        }

        scheme = ruleUri.Scheme;
        hostRule = ruleUri.Host["wildcard.".Length..];
        port = ruleUri.Port;
    }
    else
    {
        hostRule = rule[2..];
    }

    return (scheme == null || string.Equals(originUri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
        && (!port.HasValue || originUri.Port == port.Value)
        && originUri.Host.Length > hostRule.Length
        && originUri.Host.EndsWith($".{hostRule}", StringComparison.OrdinalIgnoreCase);
}

// 供集成测试项目 WebApplicationFactory<Program> 引用入口类型（与顶层语句生成的 Program 合并）
public partial class Program { }
