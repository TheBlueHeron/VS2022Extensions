using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Project = Community.VisualStudio.Toolkit.Project;
using Solution = Community.VisualStudio.Toolkit.Solution;

namespace SolutionCleanup;

/// <summary>
/// The DeleteOutputFolders command handler.
/// </summary>
[Command(PackageIds.SolutionCleanupCommand)]
internal sealed class SolutionCleanupCommand : BaseCommand<SolutionCleanupCommand>
{
    #region Objects and variables

    private const string ASTERIX = "*";
    private const string CAPTION = "Solution Cleanup";
    private const string BIN = "bin";
    private const string DOTVS = ".vs";
    private const string IISEXPRESS = "IISExpress";
    private const string LOGS = "Logs";
    private const string OBJ = "obj";
    private const string PACKAGES = "packages";
    private const string REFRESH = ".refresh";
    private const string STATUS = "Removing output folders";
    private const string TESTRESULTS = "TestResults";
    private const string TRACES = "TraceLogFiles";

    private const string cmdClean = "Build.CleanSolution";

    private const string fmtDefaultCleanup = "Running default cleanup...\r\n";
    private const string fmtDefaultCleanupDone = "Default default cleanup completed.\r\n";
    private const string fmtDeletedFile = "File {0} has been deleted.";
    private const string fmtDeletedFolder = "Folder {0} has been deleted.";
    private const string fmtDeletingFolder = "Deleting folder {0}...";
    private const string fmtDeletingFolders = "Deleting output folders in {0}...";
    private const string fmtDone = "All files and folders in {0} have been deleted.";
    private const string fmtErrorFile = "Error deleting file {0}. {1}";
    private const string fmtErrorFolder = "Error deleting folder {0}. {1}";
    private const string fmtErrorIsRefresh = "Can't delete {0}. This is a .refresh file.";
    private const string fmtErrorIsScc = "Can't delete {0}. This file is under source control.";
    private const string fmtNoProjects = "There are no projects in {0}.";

    private General mOptions;
    private DTE mDte;

    #endregion

    #region Overrides

    /// <summary>
    /// Gets a handle on the <see cref="DTE" /> and <see cref="General"/>.
    /// </summary>
    /// <returns>A <see cref="Task" /></returns>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();
        mDte = await Package.GetServiceAsync<DTE, DTE>();
        mOptions = await General.GetLiveInstanceAsync();
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
        var projects = (await VS.Solutions.GetAllProjectsAsync()).Where(p => p.IsLoaded && (!string.IsNullOrEmpty(p.FullPath))).ToList();
        var output = Package.GetOutputPane(VSConstants.SID_SVsGeneralOutputWindowPane, CAPTION);
        
        if (projects.Count == 0)
        {
            OutputMessage(string.Format(fmtNoProjects, solution.FullPath), output);
            return;
        }        

        await DeleteFilesAsync(solution, output, GetDeletables(projects));
        OutputMessage(string.Format("Removing automatically recreated files as well..."), output);
        await DeleteFilesAsync(solution, output, GetDeletables(projects));
        await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - Done!");

        if (mOptions.RunDefaultCleanup)
        {
            RunDefaultClean(output);
        }
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Parses a message to the given <see cref="IVsOutputWindowPane"/>.
    /// </summary>
    /// <param name="msg">The message to display</param>
    /// <param name="output">The <see cref="IVsOutputWindowPane"/> to use</param>
    /// <returns>A <see cref="Task"/></returns>
    [DebuggerStepThrough()]
    private void OutputMessage(string msg, IVsOutputWindowPane output)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        output.Activate();
        output.OutputString($"{msg}\r\n");
    }

    /// <summary>
    /// Executes the "Build.CleanSolution" command.
    /// </summary>
    /// <param name="output">The <see cref="IVsOutputWindowPane" /> to use</param>
    /// <returns>A <see cref="Task"/></returns>
    private void RunDefaultClean(IVsOutputWindowPane output)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        OutputMessage(fmtDefaultCleanup, output);
        mDte.ExecuteCommand(cmdClean);
        OutputMessage(fmtDefaultCleanupDone, output);
    }

    #region IO

    /// <summary>
    /// Returns a collection of folders that can be deleted.
    /// </summary>
    /// <param name="projects">The projects to search through</param>
    /// <returns>A <see cref="List{DirectoryInfo}" /></returns>
    private List<DirectoryInfo> GetDeletables(IEnumerable<Project> projects)
    {
        List<DirectoryInfo> deletables = [];

        foreach (var p in projects.ToList())
        {
            var projectDirectory = new DirectoryInfo(Path.GetDirectoryName(p.FullPath));

            deletables.AddRange([.. projectDirectory.GetDirectories(ASTERIX, SearchOption.AllDirectories)
                .Where(d =>
                    (mOptions.DeleteBinFolder && (d.Name.Equals(BIN) || d.Name.Equals(OBJ))) ||
                    (mOptions.DeleteDotVsFolder && d.Name.Equals(DOTVS)) ||
                    (mOptions.DeletePackagesFolder && d.Name.Equals(PACKAGES)) ||
                    (mOptions.DeleteTestResultsFolder && d.Name.Equals(TESTRESULTS))).Distinct(new DirectoryInfoComparer())]);
            if (mOptions.DeleteIISExpressLogsFolder)
            {
                var folder = GetIISExpressLogsFolder();
                if (folder is not null)
                {
                    deletables.Add(folder);
                }
            }
            if (mOptions.DeleteIISExpressTraceLogFilesFolder)
            {
                var folder = GetIISExpressTraceLogFilesFolder();
                if (folder is not null)
                {
                    deletables.Add(folder);
                }
            }
        };
        return deletables;
    }

    /// <summary>
    /// Deletes all files and folders that were marked as deletable.
    /// </summary>
    /// <param name="solution">The active <see cref="Solution" /></param>
    /// <param name="output">The <see cref="IVsOutputWindowPane"/> to use</param>
    /// <param name="folders">The <see cref="DirectoryInfo"/>s of the deletable folders</param>
    /// <returns>A <see cref="Task"/></returns>
    private async Task DeleteFilesAsync(Solution solution, IVsOutputWindowPane output, List<DirectoryInfo> folders)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        OutputMessage(string.Format(fmtDeletingFolders, solution.FullPath), output);
        foreach (var folder in folders)
        {
            var files = folder.GetFiles(ASTERIX, SearchOption.AllDirectories).ToList();
            var idx = 0;
            var count = files.Count;

            OutputMessage(string.Format(fmtDeletingFolder, folder.FullName), output);
            await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - {folder.Name}");
            foreach (var file in files)
            {
                if (file.FullName.EndsWith(REFRESH))
                {
                    OutputMessage(string.Format(fmtErrorIsRefresh, file.FullName), output);
                }
                else if (mDte.SourceControl.IsItemUnderSCC(file.FullName))
                {
                    OutputMessage(string.Format(fmtErrorIsScc, file.FullName), output);
                }
                else
                {
                    try
                    {
                        file.Delete();
                        OutputMessage(string.Format(fmtDeletedFile, file.FullName), output);
                    }
                    catch (IOException ex)
                    {
                        OutputMessage(string.Format(fmtErrorFile, file.FullName, ex.Message), output);
                    }
                    catch (Exception ex)
                    {
                        OutputMessage(string.Format(fmtErrorFile, file.FullName, ex.Message), output);
                    }
                }
                await VS.StatusBar.ShowProgressAsync(STATUS, ++idx, count);
            }
        }
        foreach (var folder in folders)
        {
            DeleteFolderRecursive(folder, output);
        }
        OutputMessage(string.Format(fmtDone, solution.FullPath), output);
    }

    private void DeleteFolderRecursive(DirectoryInfo folder, IVsOutputWindowPane output)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            foreach (var subFolder in folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly))
            {
                DeleteFolderRecursive(subFolder, output);
            }
            if (!folder.GetFiles(ASTERIX, SearchOption.TopDirectoryOnly).Any() && !folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly).Any())
            {
                try
                {
                    folder.Delete(false);
                    OutputMessage(string.Format(fmtDeletedFolder, folder.FullName), output);
                }
                catch (IOException ex)
                {
                    OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message), output);
                }
                catch (Exception ex)
                {
                    OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message), output);
                }
            }
        }
        catch (Exception ex)
        {
            OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message), output);
        }
    }


    /// <summary>
    /// Returns the folder of the IIS Express log.
    /// </summary>
    /// <returns>A <see cref="DirectoryInfo"/> if the folder exists, else null</returns>
    private static DirectoryInfo GetIISExpressLogsFolder()
    {
        var fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IISEXPRESS, LOGS);
        return Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : null;
    }

    /// <summary>
    /// Returns the folder of the IIS Express trace log.
    /// </summary>
    /// <returns>A <see cref="DirectoryInfo"/> if the folder exists, else null</returns>
    private static DirectoryInfo GetIISExpressTraceLogFilesFolder()
    {
        var fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IISEXPRESS, TRACES);
        return Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : null;
    }

    #endregion

    #endregion
}
