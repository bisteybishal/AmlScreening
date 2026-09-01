using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

const string connectionVariable = "AML_MIGRATION_CONNECTION";
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project AmlScreening.DatabaseMigrator -- <script.sql>");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable(connectionVariable);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"Set the {connectionVariable} environment variable before running a migration.");
    return 2;
}

var scriptPath = Path.GetFullPath(args[0]);
if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"Migration script not found: {scriptPath}");
    return 2;
}

try
{
    var script = await File.ReadAllTextAsync(scriptPath);
    var batches = Regex.Split(script, @"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
        .Where(batch => !string.IsNullOrWhiteSpace(batch));

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    foreach (var batch in batches)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = batch;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"Applied {Path.GetFileName(scriptPath)} successfully.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Migration failed: {exception.Message}");
    return 1;
}
