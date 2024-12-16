using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Project = Community.VisualStudio.Toolkit.Project;
using Solution = Community.VisualStudio.Toolkit.Solution;
using Thread = System.Threading.Thread;

namespace SolutionCleanup;

/// <summary>
/// The DeleteOutputFolders command handler.
/// </summary>
[Command(PackageIds.SolutionCleanupCommand)]
internal sealed class SolutionCleanupCommand : CleanupCommand<SolutionCleanupCommand>
{
    #region Objects and variables

    private const int WAITTIME = 10000;
    private const string cmdClean = "Build.CleanSolution";
    private const string msgNoSolution = "There is no solution active.\r\n";

    #endregion

    #region Overrides

    /// <inheritdoc/>
    /// <remarks>Also subscribes to the solution's BeforeClosing event, if <see cref="General.RunOnClose"/> is true</remarks>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        mDte.Events.SolutionEvents.BeforeClosing -= ExecuteOnClose;

        if (mOptions.RunOnClose)
        {
            mDte.Events.SolutionEvents.BeforeClosing += ExecuteOnClose;
        }
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="e">The <see cref="OleMenuCmdEventArgs"/></param>
    /// <returns>A <see cref="Task"/></returns>
    protected async override Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        
        mSolution = await VS.Solutions.GetCurrentSolutionAsync();

        if (mSolution is null || string.IsNullOrEmpty(mSolution.FullPath))
        {
            await OutputMessageAsync(msgNoSolution);
            return;
        }

        mProjects = (await VS.Solutions.GetAllProjectsAsync()).Where(p => !string.IsNullOrEmpty(p.FullPath));
        
        if (!mProjects.Any())
        {
            await OutputMessageAsync(string.Format(fmtNoProjects, mSolution.FullPath));
            return;
        }

        if (mOptions.RunDefaultCleanup)
        {
            mDte.Events.BuildEvents.OnBuildDone += OnBuildDone;
            RunDefaultClean();
        }
        else
        {
            await RunSolutionCleanAsync(mSolution, mProjects, e is null? 0 : WAITTIME);
        }
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Calls <see cref="ExecuteAsync(OleMenuCmdEventArgs)"/>. Called by the solution's BeforeClosing event.
    /// </summary>
    private void ExecuteOnClose()
    {
        _ = Task.Run(async () => { await ExecuteAsync(null); });
    }

    /// <summary>
    /// Performs <see cref="RunSolutionCleanAsync(Solution, IEnumerable{Project})"/> after a <see cref="vsBuildAction.Clean"/> with scope <see cref="vsBuildScope.vsBuildScopeSolution"/> has completed.
    /// </summary>
    /// <param name="scope">The <see cref="vsBuildScope"/></param>
    /// <param name="action">The <see cref="vsBuildAction"/></param>
    private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
    {
        if (scope == vsBuildScope.vsBuildScopeSolution && action == vsBuildAction.vsBuildActionClean)
        {
            _ = Task.Run(async () =>
            {
                await RunSolutionCleanAsync(mSolution, mProjects, WAITTIME);
            });
        }
    }

    /// <summary>
    /// Executes the "Build.CleanSolution" command.
    /// </summary>
    private void RunDefaultClean()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        mDte.ExecuteCommand(cmdClean);
    }

    /// <summary>
    /// Performs thorough cleanup on the given <see cref="Project"/>s.
    /// </summary>
    /// <param name="solution">The current <see cref="Solution"/></param>
    /// <param name="projects">The <see cref="IEnumerable{Project}"/> to clean</param>
    /// <param name="waitTime">The number of milliseconds to wait before removing the automatically regenerated files</param>
    /// <returns>A <see cref="Task"/></returns>
    private async Task RunSolutionCleanAsync(Solution solution, IEnumerable<Project> projects, int waitTime)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        mDte.Events.BuildEvents.OnBuildDone -= OnBuildDone;
        await DeleteFilesAsync(solution, GetDeletables(projects));
        if (mNuGet is not null)
        {
            await OutputMessageAsync(string.Format(msgDeleteAutoCreate, WAITTIME / 1000));

            // https://github.com/NuGet/Home/discussions/13827
            // https://github.com/search?q=IVsNuGetProjectUpdateEvents&type=code
            // https://learn.microsoft.com/en-us/nuget/visual-studio-extensibility/nuget-api-in-visual-studio#ivsnugetprojectupdateevents-interface
            
            _ = Task.Run(async () =>
            {
                Thread.Sleep(waitTime);
                await DeleteFilesAsync(solution, GetDeletables(projects));
                await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - Done!");
            });
        }
    }

    #endregion
}