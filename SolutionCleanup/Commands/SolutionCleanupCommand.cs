using System.Linq;
using EnvDTE;

namespace SolutionCleanup;

/// <summary>
/// The DeleteOutputFolders command handler.
/// </summary>
[Command(PackageIds.SolutionCleanupCommand)]
internal sealed class SolutionCleanupCommand : CleanupCommand<SolutionCleanupCommand>
{
    #region Objects and variables

    private const string cmdClean = "Build.CleanSolution";

    private const string fmtNoSolution = "There is no solution active.\r\n";

    #endregion

    #region Overrides

    /// <inheritdoc/>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();

        if (mOptions.RunOnClose)
        {
            Microsoft.VisualStudio.Shell.Events.SolutionEvents.OnBeforeCloseSolution += (s, e) => Task.Run(async () => { await ExecuteAsync(null); });
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

        var solution = await VS.Solutions.GetCurrentSolutionAsync();

        if (solution is null || string.IsNullOrEmpty(solution.FullPath))
        {
            OutputMessage(fmtNoSolution);
            return;
        }

        var projects = (await VS.Solutions.GetAllProjectsAsync()).Where(p => p.IsLoaded && (!string.IsNullOrEmpty(p.FullPath))).ToList();
        
        if (projects.Count == 0)
        {
            OutputMessage(string.Format(fmtNoProjects, solution.FullPath));
            return;
        }

        await DeleteFilesAsync(solution, GetDeletables(projects));
        OutputMessage(string.Format(fmtDeleteAutoCreate));
        await DeleteFilesAsync(solution, GetDeletables(projects));
        await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - Done!");
        if (mOptions.RunDefaultCleanup)
        {
            RunDefaultClean();
        }
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Executes the "Build.CleanSolution" command.
    /// </summary>
    private void RunDefaultClean()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        mDte.ExecuteCommand(cmdClean);
    }

    #endregion
}