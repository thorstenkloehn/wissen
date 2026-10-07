using System.ComponentModel;
using System.Diagnostics;
using Npgsql;

namespace Wissen.Infrastructure.Backup;

// Sichert und stellt die PostgreSQL-Datenbank über pg_dump und pg_restore wieder her.
public class DatabaseBackup(string connectionString)
{
    private readonly NpgsqlConnectionStringBuilder _connection = new(connectionString);

    public string Database => _connection.Database ?? _connection.Username ?? "";

    public string Host => _connection.Host ?? "localhost";

    public Task<int> BackupAsync(string file, CancellationToken cancellationToken = default) =>
        RunAsync("pg_dump", BuildBackupArguments(file), cancellationToken);

    public Task<int> RestoreAsync(string file, CancellationToken cancellationToken = default) =>
        RunAsync("pg_restore", BuildRestoreArguments(file), cancellationToken);

    public IReadOnlyList<string> BuildBackupArguments(string file) =>
    [
        .. BuildConnectionArguments(),
        "--format=custom",
        "--no-owner",
        "--file", file,
    ];

    public IReadOnlyList<string> BuildRestoreArguments(string file) =>
    [
        .. BuildConnectionArguments(),
        "--clean",
        "--if-exists",
        "--no-owner",
        "--single-transaction",
        file,
    ];

    // Das Passwort steht nie in den Argumenten (sichtbar in der Prozessliste), sondern in PGPASSWORD.
    private IReadOnlyList<string> BuildConnectionArguments() =>
    [
        "--host", Host,
        "--port", _connection.Port.ToString(),
        "--username", _connection.Username ?? "",
        "--dbname", Database,
        "--no-password",
    ];

    private async Task<int> RunAsync(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(tool) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (!string.IsNullOrEmpty(_connection.Password))
        {
            startInfo.Environment["PGPASSWORD"] = _connection.Password;
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"'{tool}' konnte nicht gestartet werden. Sind die PostgreSQL-Client-Programme installiert und im PATH?", ex);
        }

        using (process)
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            return process.ExitCode;
        }
    }
}
