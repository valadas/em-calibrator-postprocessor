using System;
using System.Diagnostics;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
[GitHubActions(
    "publish",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(Publish) })]


class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion(Framework = "net6.0", UpdateAssemblyInfo = false, NoFetch = true)] readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target LogInformation => _ => _
    .Executes(() =>
    {
        Serilog.Log.Information("GitVersion: {0}", GitVersion.MajorMinorPatch);
        Serilog.Log.Information("GitRepository: {0}", SerializationTasks.JsonSerialize(GitRepository));
    });

    Target Clean => _ => _
        .DependsOn(LogInformation)
        .Before(Restore)
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target PublishMac => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetConfiguration(Configuration)
                .SetPublishProfile("mac")
                .SetProject(Solution.GetProject("em-calibrator"))
                .SetOutput(ArtifactsDirectory / "mac"));

            if (IsUnix)
            {
                using var process = ProcessTasks.StartProcess(
                    "chmod",
                    $"a+x {ArtifactsDirectory / "mac" / "em-calibrator"}",
                    RootDirectory);
                process.AssertZeroExitCode();
                foreach (var output in process.Output)
                {
                    Serilog.Log.Information(output.Text);
                }
            }
        });

    Target PublishLinux => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetConfiguration(Configuration)
                .SetPublishProfile("linux")
                .SetProject(Solution.GetProject("em-calibrator"))
                .SetOutput(ArtifactsDirectory / "linux"));

            if (IsUnix)
            {
                using var process = ProcessTasks.StartProcess(
                    "chmod",
                    $"a+x {ArtifactsDirectory / "linux" / "em-calibrator"}",
                    RootDirectory);
                process.AssertZeroExitCode();
                foreach (var output in process.Output)
                {
                    Serilog.Log.Information(output.Text);
                }
            }
        });

    Target PublishWindows => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPublish(s => s
                .SetConfiguration(Configuration)
                .SetPublishProfile("win")
                .SetProject(Solution.GetProject("em-calibrator"))
                .SetOutput(ArtifactsDirectory / "win"));
        });

    Target Publish => _ => _
        .DependsOn(PublishMac, PublishLinux, PublishWindows);
}
