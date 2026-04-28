namespace J9_Admin.SeedData.Ddd
{
    /// <summary>
    /// 限时活动种子数据（对应实体 <see cref="DEvent"/>）
    /// 数据来源：buyu.db 导出的 ddd_event 配置
    /// 初始化策略：
    ///   1. 需要先有默认代理（DAgent），否则跳过（等 InitDbData 建好代理后再运行）
    ///   2. 按 Title 判重，未存在则插入，已存在则跳过（可重复执行）
    /// </summary>
    public static class EventSeedData
    {
        /// <summary>
        /// 初始化限时活动数据
        /// </summary>
        public static void Initialize(FreeSqlCloud fsql)
        {
            // 关联到第一个代理（默认顶级代理）。若没有代理则不写入，留给 InitDbData 先建代理。
            var firstAgent = fsql.Select<DAgent>()
                .OrderBy(a => a.Id)
                .First();

            if (firstAgent == null)
            {
                return;
            }

            var repo = fsql.GetRepository<DEvent>();
            var now = DateTime.Now;

            var events = BuildEvents(firstAgent.Id, now);

            foreach (var ev in events)
            {
                var exists = fsql.Select<DEvent>()
                    .Where(e => e.Title == ev.Title)
                    .Any();

                if (!exists)
                {
                    repo.Insert(ev);
                }
            }
        }

        /// <summary>
        /// 构造默认的限时活动列表
        /// </summary>
        private static List<DEvent> BuildEvents(long dAgentId, DateTime now)
        {
            return new List<DEvent>
            {
                new DEvent
                {
                    Title = "周末双倍积分畅玩",
                    Summary = "本周末所有电子游戏双倍积分回馈，积分可用于兑换商城精美礼品和游戏体验金。",
                    Count = 5000,
                    StartTime = now,
                    EndTime = now.AddDays(2),
                    Type = "Promotion",
                    IsEnabled = true,
                    Sort = 90,
                    BannerUrl = "https://picsum.photos/seed/event2/800/400",
                    DAgentId = dAgentId,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DEvent
                {
                    Title = "VIP特权升级月",
                    Summary = "当月VIP晋升成功的玩家，即可获得额外晋级礼金，以及专属客服1对1服务体验。",
                    Count = 2000,
                    StartTime = now.AddDays(-5),
                    EndTime = now.AddDays(25),
                    Type = "Promotion",
                    IsEnabled = true,
                    Sort = 80,
                    BannerUrl = "https://picsum.photos/seed/event3/800/400",
                    DAgentId = dAgentId,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DEvent
                {
                    Title = "首充回馈100%",
                    Summary = "新注册玩家首次充值，即可获得100%返利，最高可返1,000元，海量游戏等你来战！",
                    Count = 8000,
                    StartTime = now.AddDays(-10),
                    EndTime = now.AddDays(30),
                    Type = "Promotion",
                    IsEnabled = true,
                    Sort = 70,
                    BannerUrl = "https://picsum.photos/seed/event4/800/400",
                    DAgentId = dAgentId,
                    CreatedTime = now,
                    ModifiedTime = now,
                }
            };
        }
    }
}
