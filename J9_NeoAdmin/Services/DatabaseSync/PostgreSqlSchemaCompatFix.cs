using Npgsql;
using Serilog;

namespace J9_NeoAdmin.Services.DatabaseSync;

/// <summary>
/// 修复 PostgreSQL 中与 FreeSql AutoSyncStructure 不兼容的列类型。
/// 常见于从 Sqlite 同步或旧版 schema 迁移后，varchar 无法自动转为数值类型。
/// </summary>
public static class PostgreSqlSchemaCompatFix
{
    private sealed record ColumnFix(string Table, string Column, string TargetType, string UsingExpression);

    private static readonly ColumnFix[] Fixes =
    [
        // NeoAdmin.Blazor.dll 中 SysParam.Id 为 Int64，旧库可能为 varchar
        new("SysParam", "Id", "INT8", BuildNumericIdUsingExpression("Id")),
    ];

    /// <summary>
    /// FreeScheduler 的 TaskInfo.Status / Interval 在 SQLite 中为 varchar（如 Running、SEC），
    /// 查询时也使用字符串字面量；误转为 int4 会导致 PostgreSQL 报 22P02。
    /// </summary>
    private static readonly ColumnFix[] SchedulerStringColumnFixes =
    [
        new("SysTask", "Status", "VARCHAR(255)", BuildStatusRevertToStringExpression()),
        new("SysTask", "Interval", "VARCHAR(255)", BuildIntervalRevertToStringExpression()),
    ];

    /// <summary>
    /// 旧版 NoAdmin schema 中存在、新版 NeoAdmin.Blazor.dll 已不再写入的列，需放宽 NOT NULL。
    /// </summary>
    private sealed record NullableColumnFix(string Table, string Column);

    private static readonly NullableColumnFix[] NullableFixes =
    [
        // WriteLoginLogAsync 仅插入 Username/Type/Extra/Ip/UserAgent，不再设置 Device
        new("SysUserLoginLog", "Device"),
    ];

    public static async Task ApplyAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var fix in Fixes.Concat(SchedulerStringColumnFixes))
        {
            if (!await TableExistsAsync(connection, fix.Table))
            {
                continue;
            }

            var udtName = await GetColumnUdtNameAsync(connection, fix.Table, fix.Column);
            if (string.IsNullOrWhiteSpace(udtName))
            {
                continue;
            }

            if (IsCompatibleType(udtName, fix.TargetType))
            {
                continue;
            }

            var sql = $"""
                ALTER TABLE "public"."{fix.Table}"
                ALTER COLUMN "{fix.Column}" TYPE {fix.TargetType}
                USING ({fix.UsingExpression});
                """;

            Log.Information(
                "修复 {Table}.{Column} 列类型：{FromType} -> {TargetType}",
                fix.Table,
                fix.Column,
                udtName,
                fix.TargetType);

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var fix in NullableFixes)
        {
            if (!await TableExistsAsync(connection, fix.Table))
            {
                continue;
            }

            var isNullable = await GetColumnIsNullableAsync(connection, fix.Table, fix.Column);
            if (!string.Equals(isNullable, "NO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sql = $"""
                ALTER TABLE "public"."{fix.Table}"
                ALTER COLUMN "{fix.Column}" DROP NOT NULL;
                """;

            Log.Information(
                "放宽 {Table}.{Column} 列 NOT NULL 约束（新版 DLL 不再写入该列）",
                fix.Table,
                fix.Column);

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static bool IsCompatibleType(string udtName, string targetType) =>
        targetType.ToUpperInvariant() switch
        {
            "INT4" => string.Equals(udtName, "int4", StringComparison.OrdinalIgnoreCase),
            "INT8" => string.Equals(udtName, "int8", StringComparison.OrdinalIgnoreCase),
            "VARCHAR(255)" => string.Equals(udtName, "varchar", StringComparison.OrdinalIgnoreCase)
                || string.Equals(udtName, "text", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static string BuildIntervalRevertToStringExpression() =>
        """
        CASE
            WHEN "Interval" IS NULL THEN 'SEC'
            WHEN pg_typeof("Interval")::text = 'integer' THEN
                CASE "Interval"
                    WHEN 1 THEN 'SEC'
                    WHEN 11 THEN 'RunOnDay'
                    WHEN 12 THEN 'RunOnWeek'
                    WHEN 13 THEN 'RunOnMonth'
                    WHEN 21 THEN 'Custom'
                    ELSE 'SEC'
                END
            ELSE "Interval"::text
        END
        """;

    private static string BuildStatusRevertToStringExpression() =>
        """
        CASE
            WHEN "Status" IS NULL THEN 'Running'
            WHEN pg_typeof("Status")::text = 'integer' THEN
                CASE "Status"
                    WHEN 0 THEN 'Running'
                    WHEN 1 THEN 'Paused'
                    WHEN 2 THEN 'Completed'
                    ELSE 'Running'
                END
            ELSE "Status"::text
        END
        """;

    private static string BuildNumericIdUsingExpression(string columnName) =>
        $"""
        CASE
            WHEN "{columnName}" IS NULL OR trim("{columnName}") = '' THEN 0
            WHEN trim("{columnName}") ~ '^\d+$' THEN trim("{columnName}")::bigint
            ELSE 0
        END
        """;

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @tableName
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task<string?> GetColumnIsNullableAsync(NpgsqlConnection connection, string tableName, string columnName)
    {
        const string sql = """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @tableName
              AND column_name = @columnName
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        command.Parameters.AddWithValue("columnName", columnName);
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    private static async Task<string?> GetColumnUdtNameAsync(NpgsqlConnection connection, string tableName, string columnName)
    {
        const string sql = """
            SELECT udt_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @tableName
              AND column_name = @columnName
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        command.Parameters.AddWithValue("columnName", columnName);
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }
}
