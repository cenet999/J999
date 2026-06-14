namespace J9_NeoAdmin.SeedData;

/// <summary>
/// 预置 NeoAdmin 框架所需的 demo 演示账号（demo001～demo050）。
/// 框架 UserSeedData 只插入基础 SysUser 字段，而 J9 的 DMember 扩展列在 SQLite 中为 NOT NULL，
/// 须在本框架种子执行前先写入完整会员记录，避免启动失败。
/// </summary>
public static class SysUserDemoSeedData
{
    private const string DemoPassword = "123456";
    private const int DemoUserCount = 50;

    private static readonly string[] Nicknames =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨帆", "赵敏", "黄强", "周杰", "吴婷",
        "徐磊", "孙丽", "马超", "朱琳", "胡军", "郭佳", "何勇", "高雪", "林峰", "罗燕",
        "梁浩", "宋佳", "郑凯", "谢雨", "韩冰", "唐亮", "冯雪", "于波", "董洁", "萧然",
        "程远", "曹颖", "袁野", "邓华", "许晴", "傅强", "沈悦", "曾辉", "彭丽", "吕刚",
        "苏敏", "卢涛", "蒋欣", "蔡明", "贾玲", "丁磊", "魏晨", "薛峰", "叶青", "潘越"
    ];

    private static readonly string[] Departments =
    [
        "研发部", "产品部", "市场部", "运营部", "人事部", "财务部", "客服部", "设计部", "测试部", "行政部"
    ];

    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure(typeof(DMember));

        var demoCount = fsql.Select<DMember>()
            .Where(a => a.Username.StartsWith("demo"))
            .Count();

        if (demoCount >= DemoUserCount)
        {
            return;
        }

        if (demoCount > 0)
        {
            fsql.Delete<DMember>()
                .Where(a => a.Username.StartsWith("demo"))
                .ExecuteAffrows();
        }

        var random = new Random(20260523);
        var now = DateTime.Now;
        var demoUsers = new List<DMember>(DemoUserCount);
        var nextId = fsql.Select<DMember>().Max(a => a.Id);
        if (nextId <= 0)
        {
            nextId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 10_000;
        }

        for (var i = 0; i < DemoUserCount; i++)
        {
            var index = i + 1;
            var username = $"demo{index:D3}";
            var createdTime = now.AddDays(-random.Next(1, 365)).AddHours(-random.Next(0, 24));
            var hasLogin = random.Next(100) < 70;
            var department = Departments[i % Departments.Length];

            demoUsers.Add(new DMember
            {
                Id = nextId + index,
                Username = username,
                Nickname = Nicknames[i],
                Password = DemoPassword,
                IsEnabled = random.Next(100) >= 10,
                IsSystem = false,
                LoginTime = hasLogin
                    ? createdTime.AddDays(random.Next(0, 30)).AddHours(random.Next(0, 24))
                    : default,
                Description = department + "模拟账号",
                CreatedTime = createdTime,
                ParentId = 0,
                BrowserFingerprint = "",
                InviteCode = $"DEMO{index:D3}",
                CreditAmount = 0,
                IsRebateSwitch = true,
                UpdatedTime = createdTime,
                SyncTime = default,
                ContinuousCheckInDays = 0,
                ActivityPoint = 0,
                DAgentId = 0
            });
        }

        fsql.Insert(demoUsers).ExecuteAffrows();
    }
}
