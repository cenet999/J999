namespace J9_Admin.API;

/// <summary>
/// 日任务进度服务
/// </summary>
public class TaskProgressService
{
    private readonly FreeSqlCloud _fsql;
    private readonly ILogger<TaskProgressService> _logger;

    public TaskProgressService(FreeSqlCloud fsql, ILogger<TaskProgressService> logger)
    {
        _fsql = fsql;
        _logger = logger;
    }

    /// <summary>
    /// 更新任务进度
    /// </summary>
    /// <param name="memberId">会员ID</param>
    /// <param name="taskType">任务类型：Login, CheckIn, Recharge, PlayGame, Invite</param>
    /// <param name="incrementValue">进度增加值，如充值金额、游戏局数等</param>
    public async Task UpdateTaskProgressAsync(long memberId, string taskType, int incrementValue = 1)
    {
        try
        {
            var tasks = await _fsql.Select<Entities.DTask>()
                .Where(t => t.IsEnabled && t.TaskType == taskType)
                .ToListAsync();

            if (!tasks.Any()) return;

            foreach (var task in tasks)
            {
                var memberTask = await _fsql.Select<Entities.DMemberTask>()
                    .Where(t => t.DMemberId == memberId && t.DTaskId == task.Id && t.TaskDate.Date == DateTime.Today)
                    .FirstAsync();

                if (memberTask == null)
                {
                    memberTask = new Entities.DMemberTask
                    {
                        DMemberId = memberId,
                        DTaskId = task.Id,
                        TaskDate = DateTime.Today,
                        CurrentValue = incrementValue,
                        Status = incrementValue >= task.TargetValue ? 1 : 0
                    };
                    await _fsql.Insert(memberTask).ExecuteAffrowsAsync();
                }
                else if (memberTask.Status == 0)
                {
                    memberTask.CurrentValue += incrementValue;
                    if (memberTask.CurrentValue >= task.TargetValue)
                    {
                        memberTask.Status = 1;
                    }
                    await _fsql.Update<Entities.DMemberTask>()
                        .SetSource(memberTask)
                        .UpdateColumns(x => new { x.CurrentValue, x.Status })
                        .ExecuteAffrowsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新任务进度失败 [MemberId: {MemberId}, TaskType: {TaskType}]", memberId, taskType);
        }
    }
}
