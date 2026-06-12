using System.Reflection;
using FreeScheduler;
using FreeSql;
using J9_NeoAdmin.API;
using J9_NeoAdmin.Services;
using J9_NeoAdmin.TelegramBot;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using NeoAdmin.Blazor.Components;
using NeoAdmin.Blazor.Extensions;
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;
using Serilog;
using J9_NeoAdmin.SeedData;
using J9_NeoAdmin.Services.DatabaseSync;

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
    Log.Information("当前环境: {Environment}", environment);

    if (args.Length > 0 && string.Equals(args[0], "sync-pg-to-sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var syncConfiguration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        await PostgreSqlToSqliteSyncRunner.RunAsync(syncConfiguration, environment);
        return;
    }

    if (args.Length > 0 && string.Equals(args[0], "seed-sysuser-demo", StringComparison.OrdinalIgnoreCase))
    {
        await RunSysUserDemoSeedAsync(environment);
        return;
    }

    var builder = WebApplication.CreateBuilder(args);

    var domain = builder.Configuration["APIDomain"];
    Log.Information("API域名配置: {Domain}", domain);

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("keys"))
        .SetApplicationName("gougouchacha_bot");

    builder.Host.UseSerilog();

    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", corsBuilder =>
        {
            corsBuilder.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, allowedOrigins))
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });

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

    builder.AddNeoAdminSerilog();

    builder.Services.AddNeoUIPrimitives();
    builder.Services.AddNeoUIComponents();
    builder.Services.AddNeoAdmin(builder.Configuration, options =>
    {
        options.DataType = dbType;
        options.ConnectionString = dbConnStr;
        options.AutoSyncStructure = true;
        options.MonitorCommand = builder.Environment.IsDevelopment();
        options.SchedulerAssemblies = [Assembly.GetExecutingAssembly()];
        options.SchedulerExecuting = OnSchedulerExecuting;
        options.EnableIpWhitelist = true;
        options.LogDirectory = "Logs";
        options.LogFilePrefix = "log-";
    });
    builder.Services.AddNeoAdminApi(Assembly.GetExecutingAssembly());

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    if (!builder.Environment.IsDevelopment())
    {
        builder.Services.AddHostedService<TelegramBotService>();
        Log.Information("TelegramBot 已启用（当前环境：{Environment}）", builder.Environment.EnvironmentName);
    }
    else
    {
        Log.Information("本地调试环境，已跳过 TelegramBot 启动");
    }

    builder.Services.AddSingleton<MessageHandler>();
    builder.Services.AddSingleton<TGMessageApi>();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<J9_NeoAdmin.Utils.PerMemberAsyncGate>();

    builder.Services.AddScoped<GameBetHistorySyncService>();
    builder.Services.AddScoped<GameIconLocalizationService>();
    builder.Services.AddScoped<LocalFileUploadService>();
    builder.Services.AddScoped<AgentWeeklySettlementService>();
    builder.Services.AddHostedService<GameBetHistorySyncHostedService>();

    builder.Services.AddScoped<TransActionService>();
    builder.Services.AddScoped<GameService>();
    builder.Services.AddScoped<LoginService>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.GameApi.BuYuGameApi>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.GameApi.PgGameApi>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.GameApi.MSGameApi>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.GameApi.XHGameApi>();
    builder.Services.AddScoped<MessageService>();
    builder.Services.AddScoped<TaskProgressService>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.PayApi.Pay0Api>();
    builder.Services.AddScoped<J9_NeoAdmin.Services.PayApi.PayPOPOApi>();
    builder.Services.AddScoped<J9_NeoAdmin.Utils.SessionAgent>();

    builder.Services.Configure<ApiBehaviorOptions>(options => { options.SuppressModelStateInvalidFilter = true; });
    builder.Services.AddResponseCaching();

    var app = builder.Build();

    // PostgreSQL：FreeSql AutoSyncStructure 前修复不兼容的列类型（varchar -> 数值）
    if (dbType == DataType.PostgreSQL)
    {
        await PostgreSqlSchemaCompatFix.ApplyAsync(dbConnStr);
    }

    app.UseCors("CorsPolicy");
    // 兼容旧版 NoAdmin 大写 /Login?Redirect=（路由默认不区分大小写，须用中间件精确匹配）
    app.Use(async (ctx, next) =>
    {
        if (string.Equals(ctx.Request.Path.Value, "/Login", StringComparison.Ordinal))
        {
            var redirect = ctx.Request.Query["Redirect"].ToString();
            var target = string.IsNullOrWhiteSpace(redirect)
                ? "/login"
                : $"/login?redirect={Uri.EscapeDataString(redirect)}";
            ctx.Response.Redirect(target);
            return;
        }

        await next();
    });
    app.UseNeoAdminSerilogRequestLogging();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapStaticAssets();
    app.UseNeoAdmin();
    app.UseAntiforgery();
    app.UseResponseCaching();

    app.MapRazorComponents<J9_NeoAdmin.Components.App>()
        .AddAdditionalAssemblies(typeof(LayoutAdmin).Assembly)
        .AddInteractiveServerRenderMode();

    app.MapGet("/profile", () => new
    {
        app = "J9_NeoAdmin",
        version = "v1.0.2",
        buildTime = File.GetLastWriteTime(typeof(Program).Assembly.Location).ToString("yyyy-MM-dd HH:mm:ss"),
        serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        environment
    });

    var fsql = app.Services.GetService<IFreeSql>();
    if (fsql != null)
    {
        var deletedLoginLogCount = await fsql.Delete<SysUserLoginLog>()
            .Where(a => a.Ip != null && a.Ip != "" && a.Ip.Contains(":"))
            .ExecuteAffrowsAsync();
        Log.Information("已删除 SysUserLoginLog 表中的 IPv6 地址记录 {Count} 条", deletedLoginLogCount);

        J9_NeoAdmin.SeedData.MenuSeedData.Initialize(fsql);
        J9_NeoAdmin.SeedData.Ddd.GamePlatformSeedData.Initialize(fsql);
        J9_NeoAdmin.SeedData.Ddd.TaskSeedData.Initialize(fsql);
        J9_NeoAdmin.SeedData.Ddd.EventSeedData.Initialize(fsql);
        J9_NeoAdmin.SeedData.Ddd.NoticeSeedData.Initialize(fsql);
    }

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

static void OnSchedulerExecuting(IServiceProvider service, TaskInfo task)
{
    switch (task.Topic)
    {
        case "武林大会":
            break;
        case "攻城活动":
            break;
    }
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

static Task RunSysUserDemoSeedAsync(string environment)
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

    var activeDbProvider = configuration["ConnectionStrings:ActiveProvider"];
    var dbSection = !string.IsNullOrWhiteSpace(activeDbProvider)
        ? configuration.GetSection($"ConnectionStrings:{activeDbProvider}")
        : configuration.GetSection("ConnectionStrings");
    var dbTypeText = dbSection["DataType"] ?? configuration["ConnectionStrings:DataType"];
    var dbConnStr = dbSection["Default"] ?? configuration.GetConnectionString("Default") ?? "Data Source=buyu.db";

    if (!string.IsNullOrWhiteSpace(dbTypeText) && Enum.TryParse<DataType>(dbTypeText, true, out var parsedType))
    {
        using var fsql = new FreeSqlBuilder()
            .UseConnectionString(parsedType, dbConnStr)
            .UseAutoSyncStructure(true)
            .Build();
        SysUserDemoSeedData.Initialize(fsql);
        Log.Information("SysUser demo 种子数据已就绪");
        return Task.CompletedTask;
    }

    using (var fsql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, dbConnStr)
        .UseAutoSyncStructure(true)
        .Build())
    {
        SysUserDemoSeedData.Initialize(fsql);
    }

    Log.Information("SysUser demo 种子数据已就绪");
    return Task.CompletedTask;
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

public partial class Program { }
