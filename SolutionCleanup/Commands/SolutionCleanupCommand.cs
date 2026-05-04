using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Project = Community.VisualStudio.Toolkit.Project;
using Solution = Community.VisualStudio.Toolkit.Solution;

namespace SolutionCleanup.Commands;

/// <summary>
/// The SolutionCleanup command handler.
/// </summary>
[Command(PackageIds.SolutionCleanupCommand)]
internal sealed class SolutionCleanupCommand : CleanupCommand<SolutionCleanupCommand>
{
    #region Objects and variables

    private const string msgNoSolution = "There is no solution active.\r\n";

    #endregion

    #region Overrides

    /// <inheritdoc/>
    /// <remarks>Also subscribes to the solution's BeforeClosing event, if <see cref="General.RunOnClose"/> is true</remarks>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();
    }

    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="e">The <see cref="OleMenuCmdEventArgs"/></param>
    /// <returns>A <see cref="Task"/></returns>
    protected async override Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        if (!mBusy)
        {
            mBusy = true;

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
                await SubscribeOnBuildDoneAsync(OnBuildDone);
                await RunDefaultCleanAsync();
            }
            else
            {
                await RunSolutionCleanAsync(mSolution, mProjects);
            }
            mBusy = false;
        }
        else
        {
            await OutputMessageAsync(msgBusy);
        }
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Performs <see cref="RunSolutionCleanAsync(Solution, IEnumerable{Project})"/> after a <see cref="vsBuildAction.Clean"/> with scope <see cref="vsBuildScope.vsBuildScopeSolution"/> has completed.
    /// </summary>
    /// <param name="scope">The <see cref="vsBuildScope"/></param>
    /// <param name="action">The <see cref="vsBuildAction"/></param>
    private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
    {
        if (scope == vsBuildScope.vsBuildScopeSolution && action == vsBuildAction.vsBuildActionClean)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await RunSolutionCleanAsync(mSolution, mProjects);
            });
        }
    }

    /// <summary>
    /// Performs thorough cleanup on the given <see cref="Project"/>s.
    /// </summary>
    /// <param name="solution">The current <see cref="Solution"/></param>
    /// <param name="projects">The <see cref="IEnumerable{Project}"/> to clean</param>
    /// <returns>A <see cref="Task"/></returns>
    private async Task RunSolutionCleanAsync(Solution solution, IEnumerable<Project> projects)
    {
        await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await UnsubscribeOnBuildDoneAsync(OnBuildDone);
            await DeleteFilesAsync(solution, GetDeletables(projects));
            await OutputMessageAsync(msgDeleteAutoCreate);
            await Task.Delay(5000);
            await DeleteFilesAsync(mSolution, GetDeletables(mProjects));
            await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - Done!");
        });
    }

    #endregion
}