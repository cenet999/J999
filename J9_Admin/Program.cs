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
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", corsBuilder =>
        {
            corsBuilder.SetIsOriginAllowed(_ => true) // 允许任意完全符合规范的 Origin
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

    var shouldAutoSyncStructure = true;

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

    return !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/profile", StringComparison.OrdinalIgnoreCase);
}

// 供集成测试项目 WebApplicationFactory<Program> 引用入口类型（与顶层语句生成的 Program 合并）
public partial class Program { }
