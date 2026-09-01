using System.Diagnostics;
using Amazon.CDK;
using Amazon.JSII.Runtime.Deputy;

namespace CloudSpendGuard.Infrastructure;

public class DotNetBundler(string projectPath) : DeputyBase, ILocalBundling
{
    public bool TryBundle(string outputDir, IBundlingOptions options)
    {
        var process = Process.Start(new ProcessStartInfo(
            "dotnet",
            ["publish", projectPath, "-c", "Release", "-o", outputDir]))!;

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish failed for {projectPath} with exit code {process.ExitCode}.");
        }

        return true;
    }
}
