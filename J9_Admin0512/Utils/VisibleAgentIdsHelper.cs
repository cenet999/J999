namespace J9_Admin.Utils;

/// <summary>
/// 后台「普通代理账号」可见代理范围：与代理列表页一致——admin 看全部；非 admin 仅自己、直属下级、下级的直属下级。
/// </summary>
public static class VisibleAgentIdsHelper
{
    /// <param name="agents">当前全量代理列表（内存中计算父子关系）。</param>
    /// <param name="isAdmin">为 true 时返回全部代理 Id。</param>
    /// <param name="currentAgentId">当前登录账号绑定的代理 Id（<see cref="SessionAgent.GetAgentId"/>）。</param>
    public static HashSet<long> Build(IList<DAgent> agents, bool isAdmin, long currentAgentId)
    {
        if (isAdmin)
            return agents.Select(a => a.Id).ToHashSet();

        if (currentAgentId <= 0)
            return [];

        var childAgentIds = agents.Where(a => a.ParentId == currentAgentId).Select(a => a.Id).ToList();
        var grandChildAgentIds = agents.Where(a => childAgentIds.Contains(a.ParentId)).Select(a => a.Id).ToList();

        return new[] { currentAgentId }
            .Concat(childAgentIds)
            .Concat(grandChildAgentIds)
            .ToHashSet();
    }
}
