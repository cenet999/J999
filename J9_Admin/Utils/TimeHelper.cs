using System.Runtime.InteropServices;

namespace J9_Admin.Utils;

/// <summary>
/// 北京时间（注单/API）与 Unix 秒、本机展示等常用转换。
/// </summary>
public static class TimeHelper
{
    private static readonly TimeZoneInfo _beijingTz = ResolveBeijingTimeZone();

    public static TimeZoneInfo BeijingTz => _beijingTz;

    public static DateTime BeijingNow() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _beijingTz).DateTime;

    /// <summary>北京时间墙上 <see cref="DateTimeKind.Unspecified"/> → Unix 秒。</summary>
    public static long BeijingToUnix(DateTime wallUnspecified)
    {
        var unspecified = DateTime.SpecifyKind(wallUnspecified, DateTimeKind.Unspecified);
        var offset = _beijingTz.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUnixTimeSeconds();
    }

    /// <summary>Unix 秒 → 北京时间墙上（<see cref="DateTimeKind.Unspecified"/>）。</summary>
    public static DateTime UnixToBeijing(long unixSeconds)
    {
        var utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return TimeZoneInfo.ConvertTime(utc, _beijingTz).DateTime;
    }

    /// <summary>当前 UTC 的 Unix 秒。</summary>
    public static long UtcUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>按本机本地时区解读墙上时间 → Unix 秒。</summary>
    public static long LocalToUnix(DateTime wall)
    {
        var local = wall.Kind switch
        {
            DateTimeKind.Utc => wall.ToLocalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(wall, DateTimeKind.Local),
            _ => wall
        };

        return new DateTimeOffset(local).ToUnixTimeSeconds();
    }

    public static DateTime UnixToLocal(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;

    private static TimeZoneInfo ResolveBeijingTimeZone()
    {
        var ids = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "China Standard Time", "Asia/Shanghai" }
            : new[] { "Asia/Shanghai", "China Standard Time" };

        foreach (var id in ids)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Shanghai",
            TimeSpan.FromHours(8),
            "Asia/Shanghai",
            "Asia/Shanghai");
    }
}
