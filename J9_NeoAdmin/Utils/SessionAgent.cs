using Microsoft.AspNetCore.Http;
using NeoAdmin.Blazor.Core.Identity;
using NeoAdmin.Blazor.Services;

namespace J9_NeoAdmin.Utils;

public class SessionAgent
{
    private readonly IFreeSql _fsql;
    private readonly NeoAdminAuthService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SessionAgent(IFreeSql freeSql, NeoAdminAuthService authService, IHttpContextAccessor httpContextAccessor)
    {
        _fsql = freeSql;
        _authService = authService;
        _httpContextAccessor = httpContextAccessor;
    }

    public long GetAgentId()
    {
        try
        {
            var user = GetCurrentUserSync();
            if (user == null)
            {
                return 0;
            }

            var member = _fsql.Select<DMember>().Where(a => a.Id == user.Id).First();
            return member?.DAgentId ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public string GetHomeUrl()
    {
        try
        {
            var user = GetCurrentUserSync();
            if (user == null)
            {
                return string.Empty;
            }

            var member = _fsql.Select<DMember>()
                .Include(a => a.DAgent)
                .Where(a => a.Id == user.Id)
                .First();

            return member?.DAgent?.HomeUrl ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private UserSummaryResponse? GetCurrentUserSync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var token = httpContext.Request.Cookies[NeoAdminBlazorUser.AuthCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            var headerToken = httpContext.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerToken))
            {
                token = headerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? headerToken[7..]
                    : headerToken;
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return _authService.CheckAsync(token).GetAwaiter().GetResult() is { Succeeded: true, Data: not null } result
            ? result.Data
            : null;
    }
}
