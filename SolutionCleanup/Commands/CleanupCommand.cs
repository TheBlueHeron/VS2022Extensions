using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EnvDTE;
using OutputWindowPane = Community.VisualStudio.Toolkit.OutputWindowPane;
using Project = Community.VisualStudio.Toolkit.Project;
using Solution = Community.VisualStudio.Toolkit.Solution;

namespace SolutionCleanup;

/// <summary>
/// Base class for the cleanup commands.
/// </summary>
/// <typeparam name="T">The type of the command</typeparam>
internal abstract class CleanupCommand<T> : BaseCommand<T> where T : CleanupCommand<T>, new()
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
    
    private const string fmtDeletedFile = "File {0} has been deleted.";
    private const string fmtDeletedFolder = "Folder {0} has been deleted.";
    private const string fmtDeletedFolders = "Output folders in {0} have been deleted.";
    private const string fmtDeletingFolder = "Deleting folder {0}...";
    private const string fmtDeletingFolders = "Deleting output folders in {0}...";
    private const string fmtErrorFile = "Error deleting file {0}. {1}";
    private const string fmtErrorFolder = "Error deleting folder {0}. {1}";
    private const string fmtErrorIsRefresh = "Can't delete {0} because it is a .refresh file.";
    private const string fmtErrorIsScc = "Can't delete {0} because it is under source control.";
    protected const string fmtNoProjects = "There are no projects in {0}.";

    protected const string msgBusy = "An operation is already running.";
    protected const string msgDeleteAutoCreate = "Removing automatically recreated files after waiting a few seconds...";
    protected const string msgNoNuGetHandle = "Couldn't get a handle on NuGet restore, so some files may have been restored.";

    protected bool mBusy;
    protected DTE mDte;
    protected General mOptions;
    protected OutputWindowPane mOutput;
    protected IEnumerable<Project> mProjects;
    protected Solution mSolution;

    #endregion

    #region Overrides

    /// <summary>
    /// Gets instances of the <see cref="DTE"/>, <see cref="IVsOutputWindowPane"/>, <see cref="IVsSolutionRestoreService"/>, <see cref="IVsSolutionRestoreStatusProvider"/> and <see cref="General"/> objects.
    /// </summary>
    /// <returns>A <see cref="Task" /></returns>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();

        mDte = await Package.GetServiceAsync<DTE, DTE>();
        mOptions = await General.GetLiveInstanceAsync();
        mOutput = await VS.Windows.CreateOutputWindowPaneAsync(CAPTION);
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Parses a message to the configured <see cref="IVsOutputWindowPane"/>.
    /// </summary>
    /// <param name="msg">The message to display</param>
    /// <returns>A <see cref="Task"/></returns>
    [DebuggerStepThrough()]
    protected async Task OutputMessageAsync(string msg)
    {
        await mOutput.ActivateAsync();
        await mOutput.WriteLineAsync(msg);
    }

    /// <summary>
    /// Returns a collection of folders that can be deleted based on the <see cref="General"/> settings and provided <see cref="Project"/>s.
    /// </summary>
    /// <param name="projects">The <see cref="Project"/>s to search through</param>
    /// <returns>A <see cref="List{DirectoryInfo}"/></returns>
    protected List<DirectoryInfo> GetDeletables(IEnumerable<Project> projects)
    {
        List<DirectoryInfo> deletables = [];

        foreach (var p in projects.ToList())
        {
            var projectRoot = Path.GetDirectoryName(p.FullPath);

            if (mOptions.DeleteBinFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(projectRoot, BIN)));
                deletables.Add(new DirectoryInfo(Path.Combine(projectRoot, OBJ)));
            }
            if (mOptions.DeleteDotVsFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(projectRoot, DOTVS)));
            }
            if (mOptions.DeletePackagesFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(projectRoot, PACKAGES)));
            }
            if (mOptions.DeleteTestResultsFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(projectRoot, TESTRESULTS)));
            }
            if (mOptions.DeleteIISExpressLogsFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IISEXPRESS, LOGS)));
            }
            if (mOptions.DeleteIISExpressTraceLogFilesFolder)
            {
                deletables.Add(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IISEXPRESS, TRACES)));
            }
        };
        return [.. deletables.Where(d => d.Exists)];
    }

    /// <summary>
    /// Deletes all files and folders that were marked as deletable.
    /// </summary>
    /// <param name="solution">The active <see cref="Solution"/></param>
    /// <param name="folders">The <see cref="DirectoryInfo"/>s of the deletable folders</param>
    /// <returns>A <see cref="Task"/></returns>
    protected async Task DeleteFilesAsync(Solution solution, List<DirectoryInfo> folders)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await OutputMessageAsync(string.Format(fmtDeletingFolders, solution.FullPath));
        foreach (var folder in folders)
        {
            var files = folder.GetFiles(ASTERIX, SearchOption.AllDirectories).ToList();
            var idx = 0;
            var count = files.Count;

            await OutputMessageAsync(string.Format(fmtDeletingFolder, folder.FullName));
            await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - {folder.Name}");
            foreach (var file in files)
            {
                if (file.FullName.EndsWith(REFRESH))
                {
                    await OutputMessageAsync(string.Format(fmtErrorIsRefresh, file.FullName));
                }
                else if (mDte.SourceControl.IsItemUnderSCC(file.FullName))
                {
                    await OutputMessageAsync(string.Format(fmtErrorIsScc, file.FullName));
                }
                else
                {
                    try
                    {
                        file.Delete();
                        await OutputMessageAsync(string.Format(fmtDeletedFile, file.FullName));
                    }
                    catch (IOException ex)
                    {
                        await OutputMessageAsync(string.Format(fmtErrorFile, file.FullName, ex.Message));
                    }
                    catch (Exception ex)
                    {
                        await OutputMessageAsync(string.Format(fmtErrorFile, file.FullName, ex.Message));
                    }
                }
                await VS.StatusBar.ShowProgressAsync(STATUS, ++idx, count);
            }
        }
        foreach (var folder in folders)
        {
            await DeleteFolderRecursiveAsync(folder);
        }
        await OutputMessageAsync(string.Format(fmtDeletedFolders, solution.FullPath));
    }

    /// <summary>
    /// Recursively deletes the contents of the folder and the folder itself, provided that there are no files left.
    /// </summary>
    /// <param name="folder">The <see cref="DirectoryInfo"/></param>
    private async Task DeleteFolderRecursiveAsync(DirectoryInfo folder)
    {
        try
        {
            foreach (var subFolder in folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly))
            {
                await DeleteFolderRecursiveAsync(subFolder);
            }
            if (!folder.GetFiles(ASTERIX, SearchOption.TopDirectoryOnly).Any() && !folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly).Any())
            {
                try
                {
                    folder.Delete();
                    await OutputMessageAsync(string.Format(fmtDeletedFolder, folder.FullName));
                }
                catch (IOException ex)
                {
                    await OutputMessageAsync(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
                }
                catch (Exception ex)
                {
                    await OutputMessageAsync(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
        }
    }

    /// <summary>
    /// Executes the "Build.CleanSolution" command.
    /// </summary>
    protected void RunDefaultClean()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        mDte.ExecuteCommand(cmdClean);
    }

    #endregion
}