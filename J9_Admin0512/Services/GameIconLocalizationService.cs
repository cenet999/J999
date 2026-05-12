using System.Net;

namespace J9_Admin.Services;

/// <summary>
/// 将 DGame.Icon 中的外链图片下载到本地 wwwroot/game，并回写为本站地址。
/// </summary>
public class GameIconLocalizationService
{
    private static readonly string[] KnownImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico", ".avif"];

    private readonly AdminContext _adminContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<GameIconLocalizationService> _logger;

    public GameIconLocalizationService(
        AdminContext adminContext,
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment,
        ILogger<GameIconLocalizationService> logger)
    {
        _adminContext = adminContext;
        _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    public async Task<GameIconLocalizationResult> LocalizeExternalIconsAsync(CancellationToken cancellationToken = default)
    {
        var result = new GameIconLocalizationResult();
        var fsql = _adminContext.Orm;

        var apiDomain = NormalizeDomain(_configuration["APIDomain"]);
        var webRootPath = string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _webHostEnvironment.WebRootPath;
        var gameRootPath = Path.Combine(webRootPath, "game");
        Directory.CreateDirectory(gameRootPath);

        var games = await fsql.Select<DGame>()
            .Where(g => g.Icon != null && g.Icon != "")
            .ToListAsync(cancellationToken);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Scanned++;

            var icon = game.Icon?.Trim();
            if (string.IsNullOrWhiteSpace(icon))
            {
                result.SkippedEmpty++;
                continue;
            }

            if (!Uri.TryCreate(icon, UriKind.Absolute, out var iconUri))
            {
                result.SkippedNonAbsolute++;
                continue;
            }

            if (!IsHttpScheme(iconUri))
            {
                result.SkippedNonHttp++;
                continue;
            }

            if (IsSameDomain(apiDomain, iconUri))
            {
                result.SkippedCurrentDomain++;
                continue;
            }

            try
            {
                var relativePath = BuildRelativePath(iconUri);
                var localFilePath = Path.Combine(gameRootPath, relativePath);
                localFilePath = ResolveExistingLocalizedPath(localFilePath);
                var localDirectory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrWhiteSpace(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                if (!File.Exists(localFilePath))
                {
                    using var response = await httpClient.GetAsync(iconUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        result.Failed++;
                        result.Failures.Add($"GameId={game.Id}, Url={icon}, HttpStatus={(int)response.StatusCode}");
                        _logger.LogWarning("下载游戏图标失败，GameId={GameId}, Url={Url}, StatusCode={StatusCode}", game.Id, icon, (int)response.StatusCode);
                        continue;
                    }

                    var finalFilePath = EnsureFileExtension(localFilePath, response.Content.Headers.ContentType?.MediaType);
                    if (!string.Equals(finalFilePath, localFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        localFilePath = finalFilePath;
                        localDirectory = Path.GetDirectoryName(localFilePath);
                        if (!string.IsNullOrWhiteSpace(localDirectory))
                        {
                            Directory.CreateDirectory(localDirectory);
                        }
                    }

                    await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await responseStream.CopyToAsync(fileStream, cancellationToken);
                    result.Downloaded++;
                }
                else
                {
                    result.AlreadyExists++;
                }

                var relativeUrl = "/" + Path.GetRelativePath(webRootPath, localFilePath).Replace('\\', '/');
                var localIconUrl = string.IsNullOrWhiteSpace(apiDomain) ? relativeUrl : $"{apiDomain}{relativeUrl}";

                if (!string.Equals(game.Icon, localIconUrl, StringComparison.Ordinal))
                {
                    await fsql.Update<DGame>()
                        .Set(g => g.Icon, localIconUrl)
                        .Where(g => g.Id == game.Id)
                        .ExecuteAffrowsAsync(cancellationToken);

                    result.Updated++;
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Failures.Add($"GameId={game.Id}, Url={icon}, Error={ex.Message}");
                _logger.LogError(ex, "本地化游戏图标失败，GameId={GameId}, Url={Url}", game.Id, icon);
            }
        }

        return result;
    }

    private static bool IsHttpScheme(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static string NormalizeDomain(string? domain)
        => string.IsNullOrWhiteSpace(domain) ? string.Empty : domain.Trim().TrimEnd('/');

    private static bool IsSameDomain(string apiDomain, Uri iconUri)
    {
        if (string.IsNullOrWhiteSpace(apiDomain) || !Uri.TryCreate(apiDomain, UriKind.Absolute, out var apiUri))
        {
            return false;
        }

        return string.Equals(apiUri.Scheme, iconUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(apiUri.Host, iconUri.Host, StringComparison.OrdinalIgnoreCase)
            && apiUri.Port == iconUri.Port;
    }

    private static string BuildRelativePath(Uri iconUri)
    {
        var hostPart = iconUri.IsDefaultPort ? iconUri.Host : $"{iconUri.Host}_{iconUri.Port}";
        var rawPath = Uri.UnescapeDataString(iconUri.AbsolutePath ?? string.Empty).Trim('/');
        var pathSegments = rawPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizePathSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (pathSegments.Count == 0)
        {
            pathSegments.Add("index");
        }

        var fileName = pathSegments[^1];
        if (!KnownImageExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        {
            fileName += ".img";
            pathSegments[^1] = fileName;
        }

        return Path.Combine([SanitizePathSegment(hostPart), .. pathSegments]);
    }

    private static string EnsureFileExtension(string localFilePath, string? mediaType)
    {
        var currentExtension = Path.GetExtension(localFilePath);
        if (!string.Equals(currentExtension, ".img", StringComparison.OrdinalIgnoreCase))
        {
            return localFilePath;
        }

        var realExtension = mediaType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/bmp" => ".bmp",
            "image/x-icon" => ".ico",
            "image/vnd.microsoft.icon" => ".ico",
            "image/avif" => ".avif",
            _ => ".img"
        };

        return Path.ChangeExtension(localFilePath, realExtension);
    }

    private static string ResolveExistingLocalizedPath(string localFilePath)
    {
        var currentExtension = Path.GetExtension(localFilePath);
        if (!string.Equals(currentExtension, ".img", StringComparison.OrdinalIgnoreCase))
        {
            return localFilePath;
        }

        var directory = Path.GetDirectoryName(localFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(localFilePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension) || !Directory.Exists(directory))
        {
            return localFilePath;
        }

        var matchedPath = Directory.GetFiles(directory, $"{fileNameWithoutExtension}.*")
            .FirstOrDefault(path => !string.Equals(Path.GetExtension(path), ".img", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(matchedPath) ? localFilePath : matchedPath;
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "unknown";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = segment
            .Trim()
            .Select(ch => invalidChars.Contains(ch) || ch == '?' || ch == '#' ? '_' : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars);
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}

public sealed class GameIconLocalizationResult
{
    public int Scanned { get; set; }

    public int Downloaded { get; set; }

    public int Updated { get; set; }

    public int AlreadyExists { get; set; }

    public int Failed { get; set; }

    public int SkippedEmpty { get; set; }

    public int SkippedNonAbsolute { get; set; }

    public int SkippedNonHttp { get; set; }

    public int SkippedCurrentDomain { get; set; }

    public List<string> Failures { get; } = [];
}
