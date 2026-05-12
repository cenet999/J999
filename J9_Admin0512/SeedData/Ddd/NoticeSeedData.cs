namespace J9_Admin.SeedData.Ddd
{
    /// <summary>
    /// 平台公告种子数据（对应实体 <see cref="DNotice"/>）
    /// 数据来源：buyu.db 导出的 ddd_notice 配置
    /// 初始化策略：按 Title 判重，未存在则插入，已存在则跳过（可重复执行）
    /// </summary>
    public static class NoticeSeedData
    {
        /// <summary>
        /// 初始化平台公告数据
        /// </summary>
        public static void Initialize(FreeSqlCloud fsql)
        {
            var repo = fsql.GetRepository<DNotice>();
            var now = DateTime.Now;

            var notices = BuildNotices(now);

            foreach (var notice in notices)
            {
                var exists = fsql.Select<DNotice>()
                    .Where(n => n.Title == notice.Title)
                    .Any();

                if (!exists)
                {
                    repo.Insert(notice);
                }
            }
        }

        /// <summary>
        /// 构造默认的平台公告列表
        /// </summary>
        private static List<DNotice> BuildNotices(DateTime now)
        {
            return new List<DNotice>
            {
                new DNotice
                {
                    Title = "新服盛大开启！",
                    Content = "亲爱的玩家，全新【雷霆万钧】大区将于今日下午14:00准时开启！欢迎各位勇士回归征战，超多开服好礼免费相送！",
                    IsEnabled = true,
                    Sort = 10,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DNotice
                {
                    Title = "关于部分渠道充值延迟的说明",
                    Content = "由于受到网络骨干网波动影响，部分第三方支付用户的充值可能出现延迟到账的情况。技术团队正在联合支付机构加急排查，请您耐心等待。",
                    IsEnabled = true,
                    Sort = 20,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DNotice
                {
                    Title = "周末双倍爆率狂欢活动",
                    Content = "本周末起全服开启打怪经验、金币双倍掉落活动！活动时间从周五晚20:00至周日晚23:59结束，千万别错过这一波提升战力的绝佳机会！",
                    IsEnabled = true,
                    Sort = 30,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DNotice
                {
                    Title = "【停服维护更新公告】",
                    Content = "为了提供更好的游戏互动环境，各服务器群组将于明日凌晨 02:00 - 05:00 进行停机维护更新，期间将无法登录游戏，请各位合理安排下线时间。",
                    IsEnabled = true,
                    Sort = 40,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DNotice
                {
                    Title = "严厉打击违规作弊脚本的外挂声明",
                    Content = "我们始终坚持对外挂和工作室【零容忍】的原则！一经安全中心发现使用任何违规辅助工具或封包修改行为，将面临账号永久封停并公开除名的惩罚。",
                    IsEnabled = true,
                    Sort = 50,
                    CreatedTime = now,
                    ModifiedTime = now,
                }
            };
        }
    }
}
