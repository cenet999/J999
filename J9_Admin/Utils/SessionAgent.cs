using FreeSql;

namespace J9_Admin.Utils;

public class SessionAgent
{
    private readonly IAggregateRootRepository<DMember> repo;
    private readonly AdminContext adminContext;

    public SessionAgent(AdminContext adminContext, IAggregateRootRepository<DMember> repo)
    {
        this.adminContext = adminContext ?? throw new ArgumentNullException(nameof(adminContext));
        this.repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public long GetAgentId()
    {
        try
        {
            // 检查用户是否存在
            if (adminContext?.User == null)
            {
                return 0;
            }

            var userId = adminContext.User.Id;

            // 查询会员信息，添加空值检查
            var member = repo.Where(a => a.Id == userId).First();

            // 如果会员不存在或者 DAgentId 为空，返回0
            return member?.DAgentId ?? 0;
        }
        catch (Exception)
        {
            // 发生异常时返回0，避免程序崩溃
            return 0;
        }
    }

    public string GetHomeUrl()
    {
        try
        {
            // 检查用户是否存在
            if (adminContext?.User == null)
            {
                return "";
            }

            var userId = adminContext.User.Id;

            // 查询会员信息，添加空值检查
            var member = repo.Where(a => a.Id == userId).First();

            // 如果会员不存在或者 DAgent 为空，返回空字符串
            return member?.DAgent?.HomeUrl ?? "";
        }
        catch (Exception)
        {
            // 发生异常时返回空字符串，避免程序崩溃
            return "";
        }
    }
}