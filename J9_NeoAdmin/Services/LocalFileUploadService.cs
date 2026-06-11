using Microsoft.AspNetCore.Components.Forms;

namespace J9_NeoAdmin.Services;

/// <summary>
/// 将 Blazor 上传的文件保存到 wwwroot/uploads，并生成对外访问 URL。
/// </summary>
public sealed class LocalFileUploadService
{
    public const string UploadSubFolder = "uploads";

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalFileUploadService> _logger;

    public LocalFileUploadService(
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<LocalFileUploadService> logger)
    {
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    public string BuildPublicUrl(string fileName)
    {
        var apiDomain = _configuration["APIDomain"]?.TrimEnd('/') ?? "";
        return string.IsNullOrEmpty(apiDomain)
            ? $"/{UploadSubFolder}/{fileName}"
            : $"{apiDomain}/{UploadSubFolder}/{fileName}";
    }

    public async Task<LocalFileUploadResult?> SaveImageAsync(
        IBrowserFile file,
        long maxFileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (file.Size > maxFileSizeBytes)
        {
            return null;
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, UploadSubFolder);
        Directory.CreateDirectory(uploadsDir);

        var safeName = Path.GetFileName(file.Name);
        var fileName = $"{DateTimeOffset.Now:yyyyMMddHHmmss}_{safeName}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        try
        {
            await using var stream = File.Create(fullPath);
            await file.OpenReadStream(maxFileSizeBytes, cancellationToken).CopyToAsync(stream, cancellationToken);

            return new LocalFileUploadResult
            {
                FileName = fileName,
                RelativeUrl = $"/{UploadSubFolder}/{fileName}",
                PublicUrl = BuildPublicUrl(fileName),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存上传文件失败：{OriginFileName}", file.Name);
            return null;
        }
    }
}

public sealed class LocalFileUploadResult
{
    public required string FileName { get; init; }
    public required string RelativeUrl { get; init; }
    public required string PublicUrl { get; init; }
}
