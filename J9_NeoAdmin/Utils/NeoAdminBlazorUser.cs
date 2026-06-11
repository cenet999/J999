using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;
using NeoAdmin.Blazor.Core.Identity;
using NeoAdmin.Blazor.Entities;

namespace J9_NeoAdmin.Utils;

public static class NeoAdminBlazorUser
{
    public const string AuthCookieName = "NeoAdmin.Auth";

    public static async Task<UserSummaryResponse?> GetCurrentUserAsync(
        IHttpContextAccessor httpContextAccessor,
        NeoAdminAuthService authService,
        IJSRuntime? jsRuntime = null)
    {
        UserSummaryResponse? user = await GetCurrentUserFromTokenAsync(
            httpContextAccessor.HttpContext?.Request.Cookies[AuthCookieName],
            authService);
        if (user != null)
            return user;

        if (jsRuntime == null)
            return null;

        try
        {
            string? token = await jsRuntime.InvokeAsync<string?>(
                "eval",
                "window.neoAdminAuth?.getToken?.() || null");
            return await GetCurrentUserFromTokenAsync(token, authService);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (JSException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // 预渲染或电路未就绪时无法调用 JS interop
            return null;
        }
    }

    public static async Task<bool> IsSiteAdminAsync(
        IHttpContextAccessor httpContextAccessor,
        NeoAdminAuthService authService,
        IJSRuntime? jsRuntime = null,
        IFreeSql? freeSql = null)
    {
        UserSummaryResponse? user = await GetCurrentUserAsync(httpContextAccessor, authService, jsRuntime);
        if (user == null)
            return false;

        if (string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase))
            return true;

        if (freeSql == null)
            return false;

        List<long> roleIds = await freeSql.Select<SysRoleUser>()
            .Where(ru => ru.UserId == user.Id)
            .ToListAsync(ru => ru.RoleId);

        if (roleIds.Count == 0)
            return false;

        return await freeSql.Select<SysRole>()
            .Where(r => roleIds.Contains(r.Id) && r.IsAdministrator)
            .AnyAsync();
    }

    private static async Task<UserSummaryResponse?> GetCurrentUserFromTokenAsync(
        string? token,
        NeoAdminAuthService authService)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        NeoAdmin.Blazor.Core.Identity.ApiResult<UserSummaryResponse> result = await authService.CheckAsync(token);
        return result.Succeeded ? result.Data : null;
    }
}
