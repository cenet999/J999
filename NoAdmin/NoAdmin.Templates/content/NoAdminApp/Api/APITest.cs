using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using NoAdminApp.API.DTO;

namespace NoAdminApp.API;

/// <summary>
/// 轻量 API smoke helper。
/// 这个文件放在 Web 项目里，因此不依赖 MSTest / RestSharp / Moq。
/// </summary>
public sealed class APITest
{
    private readonly HttpClient _client;

    /// <summary>
    /// 初始化客户端
    /// </summary>
    public APITest(HttpClient? client = null, string? baseUrl = null)
    {
        _client = client ?? new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? "http://localhost:5038")
        };
    }

    /// <summary>
    /// 登录获取令牌
    /// </summary>
    public async Task<string?> LoginAndGetTokenAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/login/@Login", new LoginRequest
        {
            Username = username,
            Password = password
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }

        if (data.TryGetProperty("token", out var tokenElement))
        {
            var token = tokenElement.GetString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return token;
        }

        var stringToken = data.ValueKind == JsonValueKind.String ? data.GetString() : null;
        if (!string.IsNullOrWhiteSpace(stringToken))
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stringToken);
        }

        return stringToken;
    }

    /// <summary>
    /// 检查登录状态
    /// </summary>
    public async Task<string> CheckAsync(CancellationToken cancellationToken = default)
    {
        return await _client.GetStringAsync("/api/login/@Check", cancellationToken);
    }

    /// <summary>
    /// 提交注册请求
    /// </summary>
    public async Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/login/@Register", request, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
