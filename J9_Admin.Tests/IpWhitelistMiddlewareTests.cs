using FreeSql;
using J9_Admin.Entities;
using J9_Admin.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace J9_Admin.Tests;

public class IpWhitelistMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowsRequest_WhenClientIpIsInWhitelist()
    {
        await using var context = await IpWhitelistTestContext.CreateAsync();
        await context.SeedWhitelistAsync("1.2.3.4");

        var httpContext = context.CreateHttpContext("1.2.3.4");
        var nextCalled = false;
        var middleware = context.CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_Returns403_WhenClientIpIsNotInWhitelist()
    {
        await using var context = await IpWhitelistTestContext.CreateAsync();
        await context.SeedWhitelistAsync("1.2.3.4");

        var httpContext = context.CreateHttpContext("5.6.7.8");
        var nextCalled = false;
        var middleware = context.CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsApiRequest_WithoutWhitelistCheck()
    {
        await using var context = await IpWhitelistTestContext.CreateAsync();
        await context.SeedWhitelistAsync("1.2.3.4");

        var httpContext = context.CreateHttpContext("5.6.7.8", "/api/login/@Login");
        var nextCalled = false;
        var middleware = context.CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsRequest_WhenForwardedHeaderIpIsInWhitelist()
    {
        await using var context = await IpWhitelistTestContext.CreateAsync();
        await context.SeedWhitelistAsync("1.2.3.4");

        var httpContext = context.CreateHttpContext("5.6.7.8");
        httpContext.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        var nextCalled = false;
        var middleware = context.CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    private sealed class IpWhitelistTestContext : IAsyncDisposable
    {
        private readonly string _dbPath;
        private readonly ServiceProvider _serviceProvider;

        public FreeSqlCloud Cloud { get; }

        private IpWhitelistTestContext(string dbPath, FreeSqlCloud cloud, ServiceProvider serviceProvider)
        {
            _dbPath = dbPath;
            Cloud = cloud;
            _serviceProvider = serviceProvider;
        }

        public static Task<IpWhitelistTestContext> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"ip-whitelist-tests-{Guid.NewGuid():N}.db");
            var cloud = new FreeSqlCloud();
            cloud.Register("default", () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={dbPath}")
                .UseAutoSyncStructure(true)
                .Build());
            cloud.Use("default");

            var services = new ServiceCollection();
            services.AddSingleton(cloud);
            var provider = services.BuildServiceProvider();

            return Task.FromResult(new IpWhitelistTestContext(dbPath, cloud, provider));
        }

        /// <summary>
        /// 往测试数据库里插入一条启用中的 IP 白名单记录，
        /// 方便单元测试快速准备放行所需的数据。
        /// </summary>
        public async Task SeedWhitelistAsync(string ipAddress)
        {
            await Cloud.Insert(new IpWhitelist
            {
                IpAddress = ipAddress,
                Description = "test",
                IsEnabled = true,
                AccessCount = 0,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now
            }).ExecuteAffrowsAsync();
        }

        /// <summary>
        /// 构造一个最小可用的 HttpContext 供中间件测试使用。
        /// 这里直接设置 RemoteIpAddress，模拟服务器实际看到的来源 IP；
        /// 同时补上 RequestServices 和可写的 Response.Body，避免中间件执行时缺少依赖或响应流不可写。
        /// </summary>
        public DefaultHttpContext CreateHttpContext(string clientIp, string path = "/")
        {
            var context = new DefaultHttpContext
            {
                RequestServices = _serviceProvider
            };
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            return context;
        }

        public IpWhitelistMiddleware CreateMiddleware(RequestDelegate next)
        {
            return new IpWhitelistMiddleware(
                next,
                NullLogger<IpWhitelistMiddleware>.Instance,
                _serviceProvider.GetRequiredService<IServiceScopeFactory>());
        }

        public ValueTask DisposeAsync()
        {
            _serviceProvider.Dispose();
            Cloud.Dispose();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            return ValueTask.CompletedTask;
        }
    }
}
