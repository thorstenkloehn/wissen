using Wissen.Infrastructure.Backup;

namespace Wissen.Tests;

public class DatabaseBackupTests
{
    private const string ConnectionString = "Host=db.example;Port=5433;Database=wissen;Username=anna;Password=geheim";

    [Fact]
    public void BackupArguments_ContainConnectionAndFile()
    {
        var arguments = new DatabaseBackup(ConnectionString).BuildBackupArguments("/tmp/a.dump");

        Assert.Equal(
            ["--host", "db.example", "--port", "5433", "--username", "anna", "--dbname", "wissen", "--no-password", "--format=custom", "--no-owner", "--file", "/tmp/a.dump"],
            arguments);
    }

    [Fact]
    public void RestoreArguments_ReplaceDatabaseInOneTransaction()
    {
        var arguments = new DatabaseBackup(ConnectionString).BuildRestoreArguments("/tmp/a.dump");

        Assert.Contains("--clean", arguments);
        Assert.Contains("--single-transaction", arguments);
        Assert.Equal("/tmp/a.dump", arguments[^1]);
    }

    [Fact]
    public void Arguments_NeverContainPassword()
    {
        var backup = new DatabaseBackup(ConnectionString);

        Assert.DoesNotContain(backup.BuildBackupArguments("a.dump"), a => a.Contains("geheim"));
        Assert.DoesNotContain(backup.BuildRestoreArguments("a.dump"), a => a.Contains("geheim"));
    }

    [Fact]
    public void Defaults_ApplyWhenHostAndPortAreMissing()
    {
        var backup = new DatabaseBackup("Database=wissen;Username=anna");

        Assert.Equal("localhost", backup.Host);
        Assert.Contains("5432", backup.BuildBackupArguments("a.dump"));
    }
}
