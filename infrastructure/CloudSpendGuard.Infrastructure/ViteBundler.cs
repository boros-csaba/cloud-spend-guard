using System.Diagnostics;
using Amazon.CDK;
using Amazon.JSII.Runtime.Deputy;

namespace CloudSpendGuard.Infrastructure;

public class ViteBundler(string projectPath) : DeputyBase, ILocalBundling
{
    public bool TryBundle(string outputDir, IBundlingOptions options)
    {
        Run("ci");
        Run("run", "build", "--", "--outDir", outputDir, "--emptyOutDir");

        return true;
    }

    private void Run(params string[] arguments)
    {
        // npm.cmd derives its own install directory from %0, which only resolves correctly
        // when cmd.exe performs the PATH lookup.
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", ["/c", "npm", .. arguments])
            : new ProcessStartInfo("npm", arguments);

        startInfo.WorkingDirectory = projectPath;

        var process = Process.Start(startInfo)!;

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"npm {string.Join(' ', arguments)} failed for {projectPath} with exit code {process.ExitCode}.");
        }
    }
}
