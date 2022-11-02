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
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Common.IO.CompressionTasks;
using Octokit;
using Nuke.Common.Tools.GitHub;
using Octokit.Internal;
using ParameterAttribute = Nuke.Common.ParameterAttribute;
using System.Text;
using System.Collections.Generic;
using System.IO;
using GlobExpressions;

[ShutdownDotNetAfterServerBuild]
[GitHubActions(
    "publish",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.Push },
    InvokedTargets = new[] { nameof(Publish) },
    FetchDepth = 0,
    EnableGitHubToken = true)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Publish);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository Repository;
    [GitVersion(Framework = "net5.0", UpdateAssemblyInfo = false, NoFetch = true)] readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target LogInformation => _ => _
    .Executes(() =>
    {
        Serilog.Log.Information("GitVersion: {0}", GitVersion.MajorMinorPatch);
        Serilog.Log.Information("GitRepository: {0}", SerializationTasks.JsonSerialize(Repository));
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
                .EnablePublishSingleFile()
                .EnableSelfContained()
                .SetRuntime("osx.10.12-x64")
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
                .EnablePublishSingleFile()
                .EnableSelfContained()
                .SetRuntime("linux-x64")
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
                .EnablePublishSingleFile()
                .EnableSelfContained()
                .SetRuntime("win-x64")
                .SetOutput(ArtifactsDirectory / "win"));
        });

    Target Publish => _ => _
        .DependsOn(PublishMac, PublishLinux, PublishWindows)
        .Produces(ArtifactsDirectory)
        .Executes(() =>
        {
            var version = GitVersion.MajorMinorPatch;

            CompressZip(ArtifactsDirectory / "mac", ArtifactsDirectory / "release" / $"em-calibrator-mac_{version}.zip");
            CompressZip(ArtifactsDirectory / "linux", ArtifactsDirectory / "release" / $"em-calibrator-linux_{version}.zip");
            CompressZip(ArtifactsDirectory / "win", ArtifactsDirectory / "release" / $"em-calibrator-win_{version}.zip");

            if (
                (!Repository.IsOnReleaseBranch() && !Repository.IsOnMainOrMasterBranch()) ||
                Repository.IsOnDevelopBranch())
            {
                Serilog.Log.Information("Not on release branch, skipping GitHub release");
                return;
            }
            
            var credentials = new Credentials(GitHubActions.Instance.Token);
            GitHubTasks.GitHubClient = new GitHubClient(
                new ProductHeaderValue(nameof(NukeBuild)),
                new InMemoryCredentialStore(credentials));

            var milestone = Repository.GetGitHubMilestone(GitVersion.MajorMinorPatch).Result;
            if (milestone is null)
            {
                Serilog.Log.Warning($"Milestones {GitVersion.MajorMinorPatch} not found, release notes will be empty.");
            }

        var prs = GitHubTasks.GitHubClient.PullRequest.GetAllForRepository(
            Repository.GetGitHubOwner(),
            Repository.GetGitHubName(),
            new PullRequestRequest
            {
                State = ItemStateFilter.Closed,
                SortProperty = PullRequestSort.Updated,
                SortDirection = SortDirection.Descending,
            })
        .Result
        .Where(pr =>
            pr.Merged == true &&
            pr.Milestone?.Title == GitVersion.MajorMinorPatch);

            // Build release notes
            var releaseNotesBuilder = new StringBuilder();
            releaseNotesBuilder.AppendLine($"# {Repository.GetGitHubName()} {milestone.Title}")
                .AppendLine("")
                .AppendLine($"A total of {prs.Count()} pull requests where merged in this release.").AppendLine();

            foreach (var group in prs.GroupBy(p => p.Labels[0]?.Name, (label, prs) => new { label, prs }))
            {
                releaseNotesBuilder.AppendLine($"## {group.label}");
                foreach (var pr in group.prs)
                {
                    releaseNotesBuilder.AppendLine($"- #{pr.Number} {pr.Title}. Thanks @{pr.User.Login}");
                }
            }

            Serilog.Log.Information(releaseNotesBuilder.ToString());

            var tag = Repository.IsOnMainOrMasterBranch() ? GitVersion.MajorMinorPatch : GitVersion.SemVer;
            GitLogger = (type, output) => Serilog.Log.Information(output);
            Git($"tag {tag}");
            Git("push --tags");

            // RELEASE
            var release = GitHubTasks.GitHubClient.Repository.Release.Create(
                Repository.GetGitHubOwner(),
                Repository.GetGitHubName(),
                new NewRelease(tag)
                {
                    Name = tag,
                    Body = releaseNotesBuilder.ToString(),
                    Draft = true,
                    Prerelease = Repository.IsOnMainOrMasterBranch() == false,
                }).Result;
            Serilog.Log.Information($"Released {release.Name} !");

            // Upload assets
            var releaseDirectory = ArtifactsDirectory / "release";
            foreach (var releaseFile in releaseDirectory.GlobFiles("*.zip"))
            {
                GitHubTasks.GitHubClient.Repository.Release.UploadAsset(
                    release,
                    new ReleaseAssetUpload
                    {
                        ContentType = "application/zip",
                        FileName = releaseFile.Name,
                        RawData = File.OpenRead(releaseFile),
                    });
                Serilog.Log.Information($"{releaseFile.Name} uploaded !");
            }

            // Close milestone
            Repository.CloseGitHubMilestone(milestone.Title, false).Wait();
            Serilog.Log.Information($"Milestone {milestone.Title} closed !");
        });
}
