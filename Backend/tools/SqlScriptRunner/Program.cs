using System.Globalization;
using Microsoft.Data.SqlClient;

const string connectionVariable = "SCMS_FLOW2_SQL_CONNECTION";
const string passwordVariable = "SCMS_FLOW2_DEMO_PASSWORD";
const string passwordHashMarker = "__FLOW2_PASSWORD_HASH__";

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project SqlScriptRunner.csproj -- <script.sql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable(connectionVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Environment variable {connectionVariable} is required.");
    return 2;
}

var scriptPath = Path.GetFullPath(args[0]);
if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"SQL script not found: {scriptPath}");
    return 2;
}

var builder = new SqlConnectionStringBuilder(connectionString)
{
    Encrypt = false,
    TrustServerCertificate = true
};

await using var connection = new SqlConnection(builder.ConnectionString);
connection.InfoMessage += (_, eventArgs) =>
{
    foreach (SqlError error in eventArgs.Errors)
    {
        Console.WriteLine($"SQL INFO: {error.Message}");
    }
};

await connection.OpenAsync();
var script = await File.ReadAllTextAsync(scriptPath);
if (script.Contains(passwordHashMarker, StringComparison.Ordinal))
{
    var demoPassword = Environment.GetEnvironmentVariable(passwordVariable);
    if (string.IsNullOrWhiteSpace(demoPassword))
    {
        Console.Error.WriteLine($"Environment variable {passwordVariable} is required by this seed script.");
        return 2;
    }

    var passwordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword, workFactor: 11);
    script = script.Replace(passwordHashMarker, passwordHash, StringComparison.Ordinal);
}
await using var command = connection.CreateCommand();
command.CommandText = script;
command.CommandTimeout = 180;

await using var reader = await command.ExecuteReaderAsync();
var resultSet = 0;
do
{
    if (reader.FieldCount == 0)
    {
        continue;
    }

    resultSet += 1;
    Console.WriteLine($"RESULT SET {resultSet}");
    Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)));
    while (await reader.ReadAsync())
    {
        var values = Enumerable.Range(0, reader.FieldCount)
            .Select(index => Format(reader.GetValue(index)));
        Console.WriteLine(string.Join(" | ", values));
    }
}
while (await reader.NextResultAsync());

return 0;

static string Format(object value) => value switch
{
    DBNull => "NULL",
    DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
    DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
    byte[] bytes => Convert.ToHexString(bytes),
    _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\r", " ").Replace("\n", " ") ?? string.Empty
};
