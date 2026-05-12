using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

namespace J9_Admin.Services.DatabaseSync;

public static class PostgreSqlToSqliteSyncRunner
{
    public static async Task RunAsync(IConfiguration configuration, string environment)
    {
        var pgConnectionString = configuration["ConnectionStrings:PostgreSQL:Default"];
        var sqliteConnectionString = configuration["ConnectionStrings:Sqlite:Default"] ?? "Data Source=buyu.db";

        ValidateConnectionString(pgConnectionString);

        var sqlitePath = GetSqlitePath(sqliteConnectionString);
        BackupSqliteDatabase(sqlitePath);

        await using var pgConnection = new NpgsqlConnection(pgConnectionString);
        await pgConnection.OpenAsync();

        await using var sqliteConnection = new SqliteConnection(sqliteConnectionString);
        await sqliteConnection.OpenAsync();

        await ExecuteSqliteNonQueryAsync(sqliteConnection, "PRAGMA foreign_keys = OFF;");

        var sourceTables = await GetPostgreSqlTablesAsync(pgConnection);
        var targetTables = await GetSqliteTablesAsync(sqliteConnection);
        var targetTableSet = new HashSet<string>(targetTables, StringComparer.OrdinalIgnoreCase);

        var commonTables = sourceTables
            .Where(targetTableSet.Contains)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (commonTables.Count == 0)
        {
            throw new InvalidOperationException("未找到 PostgreSQL 与 Sqlite 的同名表，无法执行同步。请先启动本地 Sqlite 让表结构完成初始化。");
        }

        var skippedTables = sourceTables
            .Where(table => !targetTableSet.Contains(table))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Log.Information("开始同步 PostgreSQL -> Sqlite，环境: {Environment}", environment);
        Log.Information("源表数量: {SourceCount}，目标表数量: {TargetCount}，可同步同名表数量: {CommonCount}",
            sourceTables.Count, targetTables.Count, commonTables.Count);

        if (skippedTables.Count > 0)
        {
            Log.Warning("以下 PostgreSQL 表在本地 Sqlite 中不存在，已跳过: {Tables}", string.Join(", ", skippedTables));
        }

        await using var sqliteTransaction = (SqliteTransaction)await sqliteConnection.BeginTransactionAsync();

        try
        {
            foreach (var tableName in commonTables)
            {
                var sourceColumns = await GetPostgreSqlColumnsAsync(pgConnection, tableName);
                var targetColumns = await GetSqliteColumnsAsync(sqliteConnection, sqliteTransaction, tableName);
                var targetColumnSet = new HashSet<string>(targetColumns.Select(static column => column.Name), StringComparer.OrdinalIgnoreCase);

                var commonColumns = sourceColumns
                    .Where(targetColumnSet.Contains)
                    .ToList();

                if (commonColumns.Count == 0)
                {
                    Log.Warning("表 {TableName} 没有可同步的同名列，已跳过", tableName);
                    continue;
                }

                var deletedRows = await ExecuteSqliteNonQueryAsync(
                    sqliteConnection,
                    $"DELETE FROM {QuoteSqliteIdentifier(tableName)};",
                    sqliteTransaction);

                var copiedRows = await CopyTableAsync(pgConnection, sqliteConnection, sqliteTransaction, tableName, sourceColumns, targetColumns, commonColumns);

                Log.Information("表 {TableName} 已同步，删除本地 {DeletedRows} 行，写入 {CopiedRows} 行，列数 {ColumnCount}",
                    tableName, deletedRows, copiedRows, commonColumns.Count);
            }

            await sqliteTransaction.CommitAsync();
        }
        catch
        {
            await sqliteTransaction.RollbackAsync();
            throw;
        }
        finally
        {
            await ExecuteSqliteNonQueryAsync(sqliteConnection, "PRAGMA foreign_keys = ON;");
        }

        Log.Information("PostgreSQL -> Sqlite 同步完成，本地数据库: {SqlitePath}", sqlitePath);
    }

    private static void ValidateConnectionString(string? pgConnectionString)
    {
        if (string.IsNullOrWhiteSpace(pgConnectionString))
        {
            throw new InvalidOperationException("未配置 ConnectionStrings:PostgreSQL:Default。");
        }

        if (pgConnectionString.Contains("REPLACE_WITH_USERNAME", StringComparison.OrdinalIgnoreCase) ||
            pgConnectionString.Contains("REPLACE_WITH_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
            pgConnectionString.Contains("Username=;", StringComparison.OrdinalIgnoreCase) ||
            pgConnectionString.Contains("Password=;", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "PostgreSQL 连接串仍是占位值。请填入真实用户名和密码后再执行。Supabase pooler 常见格式是 Username=postgres.<project-ref>。");
        }
    }

    private static string GetSqlitePath(string sqliteConnectionString)
    {
        var builder = new SqliteConnectionStringBuilder(sqliteConnectionString);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return Path.GetFullPath("buyu.db");
        }

        return Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(dataSource);
    }

    private static void BackupSqliteDatabase(string sqlitePath)
    {
        var sqliteDirectory = Path.GetDirectoryName(sqlitePath);
        if (!string.IsNullOrWhiteSpace(sqliteDirectory))
        {
            Directory.CreateDirectory(sqliteDirectory);
        }

        if (!File.Exists(sqlitePath))
        {
            Log.Warning("本地 Sqlite 文件不存在，将直接创建新库: {SqlitePath}", sqlitePath);
            return;
        }

        var backupPath = $"{sqlitePath}.{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}.bak";
        File.Copy(sqlitePath, backupPath, overwrite: false);
        Log.Information("已备份本地 Sqlite: {BackupPath}", backupPath);
    }

    private static async Task<List<string>> GetPostgreSqlTablesAsync(NpgsqlConnection connection)
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;

        var tables = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetSqliteTablesAsync(SqliteConnection connection)
    {
        const string sql = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        var tables = new List<string>();
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<string>> GetPostgreSqlColumnsAsync(NpgsqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tableName
            ORDER BY ordinal_position;
            """;

        var columns = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tableName", tableName);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<List<SqliteColumnInfo>> GetSqliteColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        var columns = new List<SqliteColumnInfo>();
        await using var command = new SqliteCommand($"PRAGMA table_info({QuoteSqliteIdentifier(tableName)});", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new SqliteColumnInfo(
                reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.GetInt32(3) == 1,
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return columns;
    }

    private static async Task<int> CopyTableAsync(
        NpgsqlConnection pgConnection,
        SqliteConnection sqliteConnection,
        SqliteTransaction sqliteTransaction,
        string tableName,
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<SqliteColumnInfo> targetColumns,
        IReadOnlyList<string> columnNames)
    {
        var selectSql = $"SELECT {JoinIdentifiers(columnNames, QuotePostgreSqlIdentifier)} FROM public.{QuotePostgreSqlIdentifier(tableName)};";
        await using var selectCommand = new NpgsqlCommand(selectSql, pgConnection);
        await using var reader = await selectCommand.ExecuteReaderAsync();

        var targetColumnsToInsert = targetColumns
            .Where(column => columnNames.Contains(column.Name, StringComparer.OrdinalIgnoreCase) || NeedsGeneratedValue(column, sourceColumns))
            .ToList();

        var parameterNames = targetColumnsToInsert.Select((_, index) => $"@p{index}").ToList();
        var insertSql =
            $"INSERT INTO {QuoteSqliteIdentifier(tableName)} ({JoinIdentifiers(targetColumnsToInsert.Select(static column => column.Name), QuoteSqliteIdentifier)}) VALUES ({string.Join(", ", parameterNames)});";

        await using var insertCommand = new SqliteCommand(insertSql, sqliteConnection, sqliteTransaction);
        for (var i = 0; i < parameterNames.Count; i++)
        {
            insertCommand.Parameters.Add(new SqliteParameter(parameterNames[i], DBNull.Value));
        }

        var copiedRows = 0;
        while (await reader.ReadAsync())
        {
            for (var i = 0; i < targetColumnsToInsert.Count; i++)
            {
                var targetColumn = targetColumnsToInsert[i];
                var sourceOrdinal = IndexOfColumn(columnNames, targetColumn.Name);
                if (sourceOrdinal >= 0)
                {
                    insertCommand.Parameters[i].Value = await reader.IsDBNullAsync(sourceOrdinal)
                        ? GetFallbackValue(targetColumn)
                        : NormalizeValue(reader.GetValue(sourceOrdinal));
                    continue;
                }

                insertCommand.Parameters[i].Value = GetFallbackValue(targetColumn);
            }

            await insertCommand.ExecuteNonQueryAsync();
            copiedRows++;
        }

        return copiedRows;
    }

    private static object NormalizeValue(object value) =>
        value switch
        {
            DateTimeOffset dto => dto.UtcDateTime,
            Guid guid => guid.ToString(),
            _ => value
        };

    private static bool NeedsGeneratedValue(SqliteColumnInfo column, IReadOnlyList<string> sourceColumns) =>
        column.NotNull &&
        !sourceColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase) &&
        column.DefaultValue is null;

    private static int IndexOfColumn(IReadOnlyList<string> columns, string name)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static object GetFallbackValue(SqliteColumnInfo column)
    {
        if (!column.NotNull)
        {
            return DBNull.Value;
        }

        if (!string.IsNullOrWhiteSpace(column.DefaultValue))
        {
            return DBNull.Value;
        }

        var normalizedType = column.Type.Trim().ToUpperInvariant();
        if (normalizedType.Contains("CHAR", StringComparison.Ordinal) ||
            normalizedType.Contains("CLOB", StringComparison.Ordinal) ||
            normalizedType.Contains("TEXT", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (normalizedType.Contains("DATE", StringComparison.Ordinal) ||
            normalizedType.Contains("TIME", StringComparison.Ordinal))
        {
            return DateTime.Now;
        }

        if (normalizedType.Contains("REAL", StringComparison.Ordinal) ||
            normalizedType.Contains("FLOA", StringComparison.Ordinal) ||
            normalizedType.Contains("DOUB", StringComparison.Ordinal) ||
            normalizedType.Contains("DEC", StringComparison.Ordinal) ||
            normalizedType.Contains("NUM", StringComparison.Ordinal))
        {
            return 0m;
        }

        if (normalizedType.Contains("BLOB", StringComparison.Ordinal))
        {
            return Array.Empty<byte>();
        }

        return 0;
    }

    private static async Task<int> ExecuteSqliteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction = null)
    {
        await using var command = new SqliteCommand(sql, connection, transaction);
        return await command.ExecuteNonQueryAsync();
    }

    private static string JoinIdentifiers(IEnumerable<string> names, Func<string, string> quote) =>
        string.Join(", ", names.Select(quote));

    private static string QuotePostgreSqlIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteSqliteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed record SqliteColumnInfo(string Name, string Type, bool NotNull, string? DefaultValue);
}
