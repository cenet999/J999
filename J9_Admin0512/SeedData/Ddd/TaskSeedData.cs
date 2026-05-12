using J9_Admin.Entities;

namespace J9_Admin.SeedData.Ddd
{
    /// <summary>
    /// 每日任务种子数据（对应实体 <see cref="DTask"/>）
    /// 数据来源：buyu.db 导出的 ddd_task 配置
    /// 初始化策略：按 TaskType 判重，未存在则插入，已存在则跳过（可重复执行）
    /// </summary>
    public static class TaskSeedData
    {
        /// <summary>
        /// 初始化每日任务数据
        /// </summary>
        public static void Initialize(FreeSqlCloud fsql)
        {
            var repo = fsql.GetRepository<DTask>();
            var now = DateTime.Now;

            var tasks = BuildTasks(now);

            foreach (var task in tasks)
            {
                var exists = fsql.Select<DTask>()
                    .Where(t => t.TaskType == task.TaskType && t.Title == task.Title)
                    .Any();

                if (!exists)
                {
                    repo.Insert(task);
                }
            }
        }

        /// <summary>
        /// 构造默认的每日任务列表
        /// </summary>
        private static List<DTask> BuildTasks(DateTime now)
        {
            return new List<DTask>
            {
                new DTask
                {
                    Title = "每日登录",
                    Description = "每天首次登录系统领取",
                    TaskType = "Login",
                    TargetValue = 1,
                    RewardAmount = 1m,
                    ActivityPoint = 20,
                    Icon = "flame",
                    JumpPath = "",
                    IsEnabled = true,
                    Sort = 1,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DTask
                {
                    Title = "每日签到",
                    Description = "完成每日签到打卡",
                    TaskType = "CheckIn",
                    TargetValue = 1,
                    RewardAmount = 1m,
                    ActivityPoint = 20,
                    Icon = "calendar-check",
                    JumpPath = "",
                    IsEnabled = true,
                    Sort = 2,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DTask
                {
                    Title = "每日充值",
                    Description = "每日累计充值金额大于100美元",
                    TaskType = "Recharge",
                    TargetValue = 100,
                    RewardAmount = 5m,
                    ActivityPoint = 30,
                    Icon = "coins",
                    JumpPath = "/trans/recharge",
                    IsEnabled = true,
                    Sort = 3,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DTask
                {
                    Title = "参与游戏",
                    Description = "每日累计参与5局游戏",
                    TaskType = "PlayGame",
                    TargetValue = 5,
                    RewardAmount = 2m,
                    ActivityPoint = 30,
                    Icon = "star",
                    JumpPath = "/game/list",
                    IsEnabled = true,
                    Sort = 4,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DTask
                {
                    Title = "邀请好友",
                    Description = "成功邀请1位好友注册",
                    TaskType = "Invite",
                    TargetValue = 1,
                    RewardAmount = 10m,
                    ActivityPoint = 20,
                    Icon = "star",
                    JumpPath = "/user/invite",
                    IsEnabled = true,
                    Sort = 5,
                    CreatedTime = now,
                    ModifiedTime = now,
                }
            };
        }
    }
}
