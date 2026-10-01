using System.Diagnostics;
using System.IO.Compression;

namespace WorldHub.Updater.Services;

public sealed class UpdateInstaller
{
    public void Install(
        string packagePath,
        string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            packagePath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            applicationDirectory);


        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(
                "Update package was not found.",
                packagePath);
        }

        try
        {
            using var archive =
                ZipFile.OpenRead(packagePath);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidOperationException(
                "Update package is corrupted.",
                exception);
        }


        if (!Directory.Exists(applicationDirectory))
        {
            throw new DirectoryNotFoundException(
                "Application directory was not found.");
        }


        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "WorldHubUpdate",
                Guid.NewGuid().ToString());


        Directory.CreateDirectory(
            tempDirectory);


        try
        {
            ZipFile.ExtractToDirectory(
                packagePath,
                tempDirectory);


            foreach (var file in Directory.GetFiles(
                         tempDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                var relativePath =
                    Path.GetRelativePath(
                        tempDirectory,
                        file);


                var destination =
                    Path.Combine(
                        applicationDirectory,
                        relativePath);


                var destinationDirectory =
                    Path.GetDirectoryName(
                        destination);


                if (!string.IsNullOrWhiteSpace(
                        destinationDirectory))
                {
                    Directory.CreateDirectory(
                        destinationDirectory);
                }


                File.Copy(
                    file,
                    destination,
                    true);
            }

            var applicationExe =
                Path.Combine(
                    applicationDirectory,
                    "WorldHub.App.exe");


            if (File.Exists(applicationExe))
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = applicationExe,
                        WorkingDirectory = applicationDirectory
                    });
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(
                    tempDirectory,
                    true);
            }
        }
    }
}