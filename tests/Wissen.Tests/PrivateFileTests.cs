using Wissen.Infrastructure.Backup;

namespace Wissen.Tests;

public class PrivateFileTests
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [Fact]
    public void Create_MakesFileAndNewDirectoryReadableOnlyByOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("wissen-test-").FullName;
        try
        {
            var file = Path.Combine(root, "backups", "x.dump");

            PrivateFile.Create(file);
            File.WriteAllText(file, "Inhalt");

            Assert.Equal(OwnerOnly, File.GetUnixFileMode(file));
            Assert.Equal(OwnerOnly | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(file)!));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_TightensAndEmptiesExistingFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("wissen-test-").FullName;
        try
        {
            var file = Path.Combine(root, "x.dump");
            File.WriteAllText(file, "alt");
            File.SetUnixFileMode(file, OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            PrivateFile.Create(file);

            Assert.Equal(OwnerOnly, File.GetUnixFileMode(file));
            Assert.Equal(0, new FileInfo(file).Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
