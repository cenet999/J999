namespace J9_Admin.SeedData.Ddd
{
    /// <summary>
    /// 游戏平台种子数据（对应实体 <see cref="DGamePlatform"/>）
    /// 数据来源：buyu.db 导出的 ddd_game_platform 配置
    /// 初始化策略：按 Name 判重，未存在则插入，已存在则跳过（可重复执行）
    /// 说明：平台名称需要与 _DGame.razor 同步逻辑（MS / XH）匹配
    /// </summary>
    public static class GamePlatformSeedData
    {
        /// <summary>
        /// 初始化游戏平台数据
        /// </summary>
        public static void Initialize(FreeSqlCloud fsql)
        {
            var repo = fsql.GetRepository<DGamePlatform>();
            var now = DateTime.Now;

            var platforms = BuildPlatforms(now);

            foreach (var platform in platforms)
            {
                var exists = fsql.Select<DGamePlatform>()
                    .Where(p => p.Name == platform.Name)
                    .Any();

                if (!exists)
                {
                    repo.Insert(platform);
                }
            }
        }

        /// <summary>
        /// 构造默认的游戏平台列表
        /// </summary>
        private static List<DGamePlatform> BuildPlatforms(DateTime now)
        {
            return new List<DGamePlatform>
            {
                new DGamePlatform
                {
                    Name = "美盛游戏",
                    IsEnabled = true,
                    Sort = 10,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DGamePlatform
                {
                    Name = "星汇游戏",
                    IsEnabled = true,
                    Sort = 0,
                    CreatedTime = now,
                    ModifiedTime = now,
                }
            };
        }
    }
}
