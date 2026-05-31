using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public sealed class ApiFlowTests
{
    private static readonly Uri BaseUri = new(
        Environment.GetEnvironmentVariable("NOVAADMIN_BASE_URL") ?? "http://localhost:5038");

    private static int _clientIpSeed;

    // 登录后拿到 token，并用它继续访问受保护接口。
    [Fact]
    public async Task Login_should_return_token_and_token_should_work_for_follow_up_calls()
    {
        using var client = CreateClient();
        var username = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_PASSWORD") ?? "admin";

        var token = await LoginAndGetTokenAsync(client, username, password);

        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var check = await GetJsonAsync(client, HttpMethod.Get, "/api/login/@Check");
        Assert.Equal(0, check.RootElement.GetProperty("code").GetInt32());
        Assert.Equal(username, check.RootElement.GetProperty("data").GetProperty("username").GetString());

        var whoIsUsing = await GetJsonAsync(client, HttpMethod.Get, "/api/login/@GetWhoIsUsingList?limit=12");
        Assert.Equal(0, whoIsUsing.RootElement.GetProperty("code").GetInt32());
    }

    // 注册一个新用户，并验证它能正常登录。
    [Fact]
    public async Task Register_should_create_user_and_allow_login()
    {
        using var client = CreateClient();
        var username = CreateUniqueUsername("test_user");
        var password = "P@ssw0rd123";

        var register = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@Register", new
        {
            Username = username,
            Password = password,
            Nickname = "单测用户",
            Description = "自动化测试账号"
        });

        Assert.Equal(0, register.RootElement.GetProperty("code").GetInt32());

        var token = await LoginAndGetTokenAsync(client, username, password);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    // 查询在线用户列表，确认登录态可用。
    [Fact]
    public async Task GetWhoIsUsingList_should_return_online_users()
    {
        using var client = CreateClient();
        var token = await LoginAndGetTokenAsync(
            client,
            Environment.GetEnvironmentVariable("NOVAADMIN_TEST_USERNAME") ?? "admin",
            Environment.GetEnvironmentVariable("NOVAADMIN_TEST_PASSWORD") ?? "admin");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await GetJsonAsync(client, HttpMethod.Get, "/api/login/@GetWhoIsUsingList?limit=12");
        Assert.Equal(0, response.RootElement.GetProperty("code").GetInt32());
        Assert.True(response.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1);
    }

    // 修改个人资料后，再查一次登录态确认已经更新。
    [Fact]
    public async Task UpdateMemberInfo_should_update_current_user_profile()
    {
        using var client = CreateClient();
        var (username, password) = await RegisterAndLoginUserAsync(client);

        var token = await LoginAndGetTokenAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nickname = "更新后的昵称";
        var description = "更新后的简介";

        var update = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@UpdateMemberInfo", new
        {
            Nickname = nickname,
            Description = description
        });

        Assert.Equal(0, update.RootElement.GetProperty("code").GetInt32());

        var check = await GetJsonAsync(client, HttpMethod.Get, "/api/login/@Check");
        var data = check.RootElement.GetProperty("data");
        Assert.Equal(nickname, data.GetProperty("nickname").GetString());
        Assert.Equal(description, data.GetProperty("description").GetString());
    }

    // 修改密码后，使用新密码可以继续登录。
    [Fact]
    public async Task ChangePassword_should_allow_login_with_new_password()
    {
        using var client = CreateClient();
        var username = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_PASSWORD") ?? "admin";

        var token = await LoginAndGetTokenAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newPassword = "P@ssw0rd456";
        var change = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@ChangePassword", new
        {
            OldPassword = password,
            NewPassword = newPassword
        });

        Assert.Equal(0, change.RootElement.GetProperty("code").GetInt32());

        using var loginClient = CreateClient();
        var newToken = await LoginAndGetTokenAsync(loginClient, username, newPassword);
        Assert.False(string.IsNullOrWhiteSpace(newToken));

        using var restoreClient = CreateClient();
        restoreClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        var restore = await GetJsonAsync(restoreClient, HttpMethod.Post, "/api/login/@ChangePassword", new
        {
            OldPassword = newPassword,
            NewPassword = password
        });

        Assert.Equal(0, restore.RootElement.GetProperty("code").GetInt32());
    }

    // 注销账号后，原密码应无法再登录。
    [Fact]
    public async Task DeleteAccount_should_disable_account_login()
    {
        using var client = CreateClient();
        var (username, password) = await RegisterAndLoginUserAsync(client);

        var token = await LoginAndGetTokenAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var delete = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@DeleteAccount", new
        {
            Password = password
        });

        Assert.Equal(0, delete.RootElement.GetProperty("code").GetInt32());

        var failedLogin = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@Login", new
        {
            Username = username,
            Password = password
        });

        Assert.NotEqual(0, failedLogin.RootElement.GetProperty("code").GetInt32());
        Assert.Contains("账户已被禁用", failedLogin.RootElement.GetProperty("message").GetString() ?? string.Empty);
    }

    // 上传头像接口当前未启用，返回 501 即可。
    [Fact]
    public async Task UploadAvatar_should_return_not_enabled()
    {
        using var client = CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@UploadAvatar", new
        {
            Base64 = "data:image/png;base64,AA=="
        });

        Assert.Equal(501, response.RootElement.GetProperty("code").GetInt32());
    }

    // 上传胸卡照片接口当前未启用，返回 501 即可。
    [Fact]
    public async Task UploadBadgePhoto_should_return_not_enabled()
    {
        using var client = CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@UploadBadgePhoto", new
        {
            Base64 = "data:image/png;base64,AA=="
        });

        Assert.Equal(501, response.RootElement.GetProperty("code").GetInt32());
    }

    // 发送重置密码验证码接口当前未启用，返回 501 即可。
    [Fact]
    public async Task SendResetPasswordCode_should_return_not_enabled()
    {
        using var client = CreateClient();

        var response = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@SendResetPasswordCode", new
        {
            Phone = "13800000000"
        });

        Assert.Equal(501, response.RootElement.GetProperty("code").GetInt32());
    }

    // 重置密码接口当前未启用，返回 501 即可。
    [Fact]
    public async Task ResetPassword_should_return_not_enabled()
    {
        using var client = CreateClient();

        var response = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@ResetPassword", new
        {
            Phone = "13800000000",
            Code = "123456",
            NewPassword = "P@ssw0rd789"
        });

        Assert.Equal(501, response.RootElement.GetProperty("code").GetInt32());
    }

    // 设置 AI 报警等级接口当前未启用，返回 501 即可。
    [Fact]
    public async Task SetAIAlarmLevel_should_return_not_enabled()
    {
        using var client = CreateClient();
        var token = await LoginAsAdminAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@SetAIAlarmLevel", new
        {
            Level = 2
        });

        Assert.Equal(501, response.RootElement.GetProperty("code").GetInt32());
    }

    // 文章列表接口应当可以正常返回数据。
    [Fact]
    public async Task Article_GetAll_should_return_article_list()
    {
        using var client = CreateClient();

        var response = await GetJsonAsync(client, HttpMethod.Get, "/api/article/@GetAll");
        Assert.Equal(0, response.RootElement.GetProperty("code").GetInt32());

        var data = response.RootElement.GetProperty("data");
        var totalCount = data.GetProperty("totalCount").GetInt32();
        var items = data.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(totalCount, items.Count);
    }

    private static HttpClient CreateClient(string? bearerToken = null)
    {
        var clientIp = $"10.0.0.{Interlocked.Increment(ref _clientIpSeed) % 250 + 1}";
        var client = new HttpClient(new HttpClientHandler
        {
            UseProxy = false
        })
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    private static async Task<(string Username, string Password)> RegisterAndLoginUserAsync(HttpClient client)
    {
        var username = CreateUniqueUsername("test_user");
        var password = "P@ssw0rd123";

        var register = await GetJsonAsync(client, HttpMethod.Post, "/api/login/@Register", new
        {
            Username = username,
            Password = password,
            Nickname = "单测用户",
            Description = "自动化测试账号"
        });

        Assert.Equal(0, register.RootElement.GetProperty("code").GetInt32());

        return (username, password);
    }

    private static async Task<string?> LoginAsAdminAsync(HttpClient client)
    {
        var username = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("NOVAADMIN_TEST_PASSWORD") ?? "admin";
        return await LoginAndGetTokenAsync(client, username, password);
    }

    private static async Task<string?> LoginAndGetTokenAsync(HttpClient client, string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync("/api/login/@Login", new
        {
            Username = username,
            Password = password
        }, cancellationToken);

        var json = await ReadJsonAsync(response, cancellationToken);

        Assert.Equal(0, json.RootElement.GetProperty("code").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("data", out var data));

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("token", out var tokenElement))
        {
            return tokenElement.GetString();
        }

        return data.ValueKind == JsonValueKind.String ? data.GetString() : null;
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request, cancellationToken);
        return await ReadJsonAsync(response, cancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Response is not valid JSON: {content}", ex);
        }
    }

    private static string CreateUniqueUsername(string prefix) =>
        $"{prefix}_{Guid.NewGuid().ToString("N")[..8]}";
}
