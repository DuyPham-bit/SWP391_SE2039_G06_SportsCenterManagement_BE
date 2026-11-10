// One-off copy of an existing SCMS SQL Server database into the PostgreSQL schema created by EF migrations.
// Usage: set SCMS_SOURCE_SQLSERVER and SCMS_TARGET_POSTGRES, apply migrations to the target, then `dotnet run`.
// Every target table in schema "dbo" is truncated and refilled from the same-named source table.
using Microsoft.Data.SqlClient;
using Npgsql;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var source = Environment.GetEnvironmentVariable("SCMS_SOURCE_SQLSERVER");
var target = Environment.GetEnvironmentVariable("SCMS_TARGET_POSTGRES");
if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
{
    Console.Error.WriteLine("Set SCMS_SOURCE_SQLSERVER and SCMS_TARGET_POSTGRES.");
    return 2;
}

await using var sql = new SqlConnection(source);
await sql.OpenAsync();
await using var pg = new NpgsqlConnection(target);
await pg.OpenAsync();

var columns = new Dictionary<string, List<(string Name, string Type)>>();
await using (var cmd = new NpgsqlCommand("""
    SELECT table_name, column_name, data_type FROM information_schema.columns
    WHERE table_schema = 'dbo' AND table_name <> '__EFMigrationsHistory'
    ORDER BY table_name, ordinal_position
    """, pg))
await using (var reader = await cmd.ExecuteReaderAsync())
    while (await reader.ReadAsync())
    {
        var table = reader.GetString(0);
        if (!columns.TryGetValue(table, out var list)) columns[table] = list = [];
        list.Add((reader.GetString(1), reader.GetString(2) == "USER-DEFINED" ? "citext" : reader.GetString(2)));
    }

var sourceColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
await using (var cmd = new SqlCommand("SELECT TABLE_NAME + '.' + COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo'", sql))
await using (var reader = await cmd.ExecuteReaderAsync())
    while (await reader.ReadAsync()) sourceColumns.Add(reader.GetString(0));

if (columns.Count == 0) { Console.Error.WriteLine("Target has no dbo tables; apply EF migrations first."); return 1; }

await using var tx = await pg.BeginTransactionAsync();
await Exec("SET session_replication_role = replica"); // skip FK checks while tables load in arbitrary order
await Exec("TRUNCATE " + string.Join(", ", columns.Keys.Select(Q)) + " RESTART IDENTITY CASCADE");

foreach (var (table, all) in columns)
{
    var cols = all.Where(c => sourceColumns.Contains($"{table}.{c.Name}")).ToList();
    if (cols.Count == 0) { Console.WriteLine($"skip  {table} (not in source)"); continue; }
    foreach (var missing in all.Except(cols)) Console.WriteLine($"warn  {table}.{missing.Name} not in source, using default");

    var select = $"SELECT {string.Join(", ", cols.Select(c => $"[{c.Name}]"))} FROM [dbo].[{table}]";
    await using var cmd = new SqlCommand(select, sql) { CommandTimeout = 0 };
    await using var reader = await cmd.ExecuteReaderAsync();
    await using var importer = await pg.BeginBinaryImportAsync(
        $"COPY {Q(table)} ({string.Join(", ", cols.Select(c => C(c.Name)))}) FROM STDIN (FORMAT BINARY)");
    var rows = 0;
    while (await reader.ReadAsync())
    {
        await importer.StartRowAsync();
        for (var i = 0; i < cols.Count; i++)
        {
            if (reader.IsDBNull(i)) { await importer.WriteNullAsync(); continue; }
            await importer.WriteAsync(Convert(reader.GetValue(i), cols[i].Type), cols[i].Type);
        }
        rows++;
    }
    await importer.CompleteAsync();
    Console.WriteLine($"copy  {table}: {rows}");
}

// Identity sequences must continue after the copied ids.
await using (var cmd = new NpgsqlCommand("""
    SELECT table_name, column_name FROM information_schema.columns
    WHERE table_schema = 'dbo' AND is_identity = 'YES'
    """, pg, tx))
{
    var identities = new List<(string, string)>();
    await using (var reader = await cmd.ExecuteReaderAsync())
        while (await reader.ReadAsync()) identities.Add((reader.GetString(0), reader.GetString(1)));
    foreach (var (table, column) in identities)
        await Exec($"SELECT setval(pg_get_serial_sequence('{Q(table)}', '{column}'), COALESCE((SELECT MAX({C(column)}) FROM {Q(table)}), 0) + 1, false)");
}

await Exec("SET session_replication_role = DEFAULT");
await tx.CommitAsync();
Console.WriteLine("done");
return 0;

static string Q(string table) => $"dbo.\"{table}\"";
static string C(string column) => $"\"{column}\"";

static object Convert(object value, string pgType) => pgType switch
{
    "smallint" => System.Convert.ToInt16(value),
    "integer" => System.Convert.ToInt32(value),
    "bigint" => System.Convert.ToInt64(value),
    "numeric" => System.Convert.ToDecimal(value),
    "boolean" => System.Convert.ToBoolean(value),
    "double precision" => System.Convert.ToDouble(value),
    "real" => System.Convert.ToSingle(value),
    "date" => value is DateTime d ? DateOnly.FromDateTime(d) : value,
    "citext" or "text" or "character varying" => value is string ? value : System.Convert.ToString(value)!,
    _ => value
};

async Task Exec(string text)
{
    await using var cmd = new NpgsqlCommand(text, pg, tx);
    await cmd.ExecuteNonQueryAsync();
}
