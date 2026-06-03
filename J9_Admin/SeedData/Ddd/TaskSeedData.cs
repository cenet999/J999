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

            // 实名认证排在邀请好友之上：已有库中邀请任务 Sort 可能仍为 5
            var inviteTask = fsql.Select<DTask>().Where(t => t.TaskType == "Invite").First();
            if (inviteTask != null && inviteTask.Sort < 6)
            {
                inviteTask.Sort = 6;
                inviteTask.ModifiedTime = now;
                repo.Update(inviteTask);
            }

            var realNameTask = fsql.Select<DTask>().Where(t => t.TaskType == "RealName").First();
            const string realNameDescription = "实名后每日可领";
            if (realNameTask != null)
            {
                var changed = false;
                if (realNameTask.RewardAmount != 5m)
                {
                    realNameTask.RewardAmount = 5m;
                    changed = true;
                }
                if (realNameTask.Description != realNameDescription)
                {
                    realNameTask.Description = realNameDescription;
                    changed = true;
                }
                if (changed)
                {
                    realNameTask.ModifiedTime = now;
                    repo.Update(realNameTask);
                }
            }

            if (inviteTask != null && inviteTask.RewardAmount != 50m)
            {
                inviteTask.RewardAmount = 50m;
                inviteTask.ModifiedTime = now;
                repo.Update(inviteTask);
            }

            var rechargeTask = fsql.Select<DTask>().Where(t => t.TaskType == "Recharge").First();
            const string rechargeDescription = "每日充值>100元可领取";
            if (rechargeTask != null && rechargeTask.Description != rechargeDescription)
            {
                rechargeTask.Description = rechargeDescription;
                rechargeTask.ModifiedTime = now;
                repo.Update(rechargeTask);
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
                    Description = "每日充值>100元可领取",
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
                    Title = "实名认证",
                    Description = "实名后每日可领",
                    TaskType = "RealName",
                    TargetValue = 1,
                    RewardAmount = 5m,
                    ActivityPoint = 20,
                    Icon = "id-card",
                    JumpPath = "/bind-info",
                    IsEnabled = true,
                    Sort = 5,
                    CreatedTime = now,
                    ModifiedTime = now,
                },
                new DTask
                {
                    Title = "邀请好友",
                    Description = "成功邀请1位好友注册",
                    TaskType = "Invite",
                    TargetValue = 1,
                    RewardAmount = 50m,
                    ActivityPoint = 20,
                    Icon = "star",
                    JumpPath = "/user/invite",
                    IsEnabled = true,
                    Sort = 6,
                    CreatedTime = now,
                    ModifiedTime = now,
                }
            };
        }
    }
}
