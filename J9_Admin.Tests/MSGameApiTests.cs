using FreeSql;
using J9_Admin.Services.GameApi;
using Microsoft.Extensions.Logging.Abstractions;

namespace J9_Admin.Tests;

public class MSGameApiTests
{

    /// <summary>
    /// 与 MSGameApi.GetBetHistory 行为一致，请求 MS <c>/ley/gamerecord</c>。
    /// 需能访问 <c>https://apis.msgm01.com</c>，且时间窗与线上日志一致，远端需有对应注单。
    /// 注：<c>from</c>/<c>to</c> 按 <c>zh-CN</c> 解析为北京时间墙上时钟，经 <c>Asia/Shanghai</c> 转 Unix 秒，与部署机本地时区无关。
    /// </summary>
    [Fact]
    public async Task GetBetHistory_AllowsEmptyResult_WhenRequestParamsAreValid()
    {
        using var context = await MsGameApiTestContext.CreateAsync();
        await context.SeedMemberAsync("", memberId: 1, agentId: 99);

        // 对齐线上日志中的请求窗口：2026-04-25 08:57:46 ~ 2026-04-25 14:57:46，page=1，limit=500。
        var result = await context.Api.GetBetHistory(
            "",
            "2026-04-25 08:57:46",
            "2026-04-25 14:57:46",
            500,
            1);

        Assert.NotNull(result);

        var forMember = result.FirstOrDefault(t => t.DMemberId == 1);
        if (forMember is null)
        {
            return;
        }

        Assert.Equal(TransactionType.Bet, forMember.TransactionType);
        Assert.NotEqual(0m, forMember.BetAmount);
        Assert.False(string.IsNullOrWhiteSpace(forMember.SerialNumber));
        // MS 注单可能无 roundNo，GameRound 可空
        Assert.Equal(1, forMember.DMemberId);
        Assert.Equal(99, forMember.DAgentId);
    }

    private sealed class MsGameApiTestContext : IDisposable
    {
        private readonly string _dbPath;

        public FreeSqlCloud Cloud { get; }
        public MSGameApi Api { get; }

        private MsGameApiTestContext(string dbPath, FreeSqlCloud cloud)
        {
            _dbPath = dbPath;
            Cloud = cloud;
            Api = new MSGameApi(NullLogger<MSGameApi>.Instance, cloud);
        }

        public static Task<MsGameApiTestContext> CreateAsync()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"ms-game-api-tests-{Guid.NewGuid():N}.db");
            var cloud = new FreeSqlCloud();
            cloud.Register("default", () => new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, $"Data Source={dbPath}")
                .UseAutoSyncStructure(true)
                .Build());
            cloud.Use("default");
            return Task.FromResult(new MsGameApiTestContext(dbPath, cloud));
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
