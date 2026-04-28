using FreeSql;
using J9_Admin.Services.GameApi;
using J9_Admin.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace J9_Admin.Tests;

public class XHGameApiTests
{
    [Fact]
    public void UnixToBeijing_ReturnsBeijingWallClock()
    {
        var betTime = TimeHelper.UnixToBeijing(1776987665);

        Assert.Equal(new DateTime(2026, 4, 24, 7, 41, 5), betTime);
        Assert.Equal(DateTimeKind.Unspecified, betTime.Kind);
    }

    // 测试登录游戏
    [Fact]
    public async Task GetGameUrl_ReturnsGameUrl_WhenCalledWithValidParameters()
    {
        using var context = await XhGameApiTestContext.CreateAsync();

        // 与 XHGameApiTests.rest 里的登录示例对齐：
        // 用户 13012341234，请求开 KY 棋牌 gameCode=720。
        var result = await context.Api.GetGameUrl(
            "13012341234",
            "0",
            "EVO",
            GameType.Live);
        Console.WriteLine(result);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(
            Uri.TryCreate(result, UriKind.Absolute, out var gameUri),
            $"GetGameUrl 返回的不是合法绝对地址：{result}");
        Assert.True(
            gameUri!.Scheme == Uri.UriSchemeHttp || gameUri.Scheme == Uri.UriSchemeHttps,
            $"GetGameUrl 返回了不支持的协议：{gameUri.Scheme}");
    }

    [Fact]
    public async Task GetBetHistory_ReturnsRecords_WhenCalledWithSameParametersAsXhRest()
    {
        using var context = await XhGameApiTestContext.CreateAsync();
        await context.SeedMemberAsync("13012341234", memberId: 1, agentId: 99);

        // 与 XH.rest 当前 gamerecord 示例保持一致：
        // 用户 13012341234，查询北京时间 2026-04-24 当天 00:00:00 ~ 23:59:59，limit=100，page=1。
        var result = await context.Api.GetBetHistory(
            "13012341234",
            "2026-04-24 00:00:00",
            "2026-04-24 23:59:59",
            100,
            1);

        Assert.True(
            result.Count > 0,
            "XH.rest 当前这组 gamerecord 参数可以查到数据，但 GetBetHistory 返回了空列表。");

        var first = result.First();
        Assert.Equal(TransactionType.Bet, first.TransactionType);
        Assert.NotEqual(0m, first.BetAmount);
        Assert.False(string.IsNullOrWhiteSpace(first.SerialNumber));
        Assert.False(string.IsNullOrWhiteSpace(first.GameRound));
        Assert.Equal(1776987665L, first.TransactionTime);
        Assert.Equal(1, first.DMemberId);
        Assert.Equal(99, first.DAgentId);
    }

    private sealed class XhGameApiTestContext : IDisposable
    {
        private readonly string _dbPath;

        public FreeSqlCloud Cloud { get; }
        public XHGameApi Api { get; }

        private XhGameApiTestContext(string dbPath, FreeSqlCloud cloud)
        {
            _dbPath = dbPath;
            Cloud = cloud;
            Api = new XHGameApi(NullLogger<XHGameApi>.Instance, cloud);
        }

        public static Task<XhGameApiTestContext> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"xh-game-api-tests-{Guid.NewGuid():N}.db");
            var cloud = new FreeSqlCloud();
            cloud.Register("default", () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={dbPath}")
                .UseAutoSyncStructure(true)
                .Build());
            cloud.Use("default");
            return Task.FromResult(new XhGameApiTestContext(dbPath, cloud));
        }

        public async Task SeedMemberAsync(string username, long memberId, long agentId)
        {
            await Cloud.Insert(new DMember
            {
                Id = memberId,
                Username = username,
                Nickname = username,
                Password = "pwd",
                DAgentId = agentId,
                InviteCode = $"INV-{memberId}",
                BrowserFingerprint = "test-fp",
                RegisterIp = "127.0.0.1",
                Avatar = "",
                Telegram = "",
                PhoneNumber = "",
                WithdrawPassword = "123456",
                USDTAddress = "usdt-address",
                UpdatedTime = DateTime.Now,
                CreatedTime = DateTime.Now,
                IsEnabled = true
            }).ExecuteAffrowsAsync();
        }

        public void Dispose()
        {
            Cloud.Dispose();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
