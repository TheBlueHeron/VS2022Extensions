using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
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

    protected const string fmtDeleteAutoCreate = "Removing automatically recreated files as well...";
    private const string fmtDeletedFile = "File {0} has been deleted.";
    private const string fmtDeletedFolder = "Folder {0} has been deleted.";
    private const string fmtDeletingFolder = "Deleting folder {0}...";
    private const string fmtDeletingFolders = "Deleting output folders in {0}...";
    private const string fmtErrorFile = "Error deleting file {0}. {1}";
    private const string fmtErrorFolder = "Error deleting folder {0}. {1}";
    private const string fmtErrorIsRefresh = "Can't delete {0}. This is a .refresh file.";
    private const string fmtErrorIsScc = "Can't delete {0}. This file is under source control.";
    protected const string fmtNoProjects = "There are no projects in {0}.";

    protected DTE mDte;
    protected General mOptions;
    protected IVsOutputWindowPane mOutput;

    #endregion

    #region Overrides

    /// <summary>
    /// Gets a handle on the <see cref="DTE" />, <see cref="IVsOutputWindowPane" /> and <see cref="General"/>.
    /// </summary>
    /// <returns>A <see cref="Task" /></returns>
    protected async override Task InitializeCompletedAsync()
    {
        await base.InitializeCompletedAsync();

        mDte = await Package.GetServiceAsync<DTE, DTE>();
        mOptions = await General.GetLiveInstanceAsync();
        mOutput = Package.GetOutputPane(VSConstants.SID_SVsGeneralOutputWindowPane, CAPTION);
    }

    #endregion

    #region Private methods and functions

    /// <summary>
    /// Parses a message to the configured <see cref="IVsOutputWindowPane"/>.
    /// </summary>
    /// <param name="msg">The message to display</param>
    /// <returns>A <see cref="Task"/></returns>
    [DebuggerStepThrough()]
    protected void OutputMessage(string msg)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        mOutput.Activate();
        mOutput.OutputString($"{msg}\r\n");
    }

    /// <summary>
    /// Returns a collection of folders that can be deleted based on the <see cref="General" /> settings and provided <see cref="Project" />s.
    /// </summary>
    /// <param name="projects">The <see cref="Project" />s to search through</param>
    /// <returns>A <see cref="List{DirectoryInfo}" /></returns>
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
    /// <param name="solution">The active <see cref="Solution" /></param>
    /// <param name="folders">The <see cref="DirectoryInfo"/>s of the deletable folders</param>
    /// <returns>A <see cref="Task"/></returns>
    protected async Task DeleteFilesAsync(Solution solution, List<DirectoryInfo> folders)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        OutputMessage(string.Format(fmtDeletingFolders, solution.FullPath));
        foreach (var folder in folders)
        {
            var files = folder.GetFiles(ASTERIX, SearchOption.AllDirectories).ToList();
            var idx = 0;
            var count = files.Count;

            OutputMessage(string.Format(fmtDeletingFolder, folder.FullName));
            await VS.StatusBar.ShowMessageAsync($"{Vsix.Name} - {folder.Name}");
            foreach (var file in files)
            {
                if (file.FullName.EndsWith(REFRESH))
                {
                    OutputMessage(string.Format(fmtErrorIsRefresh, file.FullName));
                }
                else if (mDte.SourceControl.IsItemUnderSCC(file.FullName))
                {
                    OutputMessage(string.Format(fmtErrorIsScc, file.FullName));
                }
                else
                {
                    try
                    {
                        file.Delete();
                        OutputMessage(string.Format(fmtDeletedFile, file.FullName));
                    }
                    catch (IOException ex)
                    {
                        OutputMessage(string.Format(fmtErrorFile, file.FullName, ex.Message));
                    }
                    catch (Exception ex)
                    {
                        OutputMessage(string.Format(fmtErrorFile, file.FullName, ex.Message));
                    }
                }
                await VS.StatusBar.ShowProgressAsync(STATUS, ++idx, count);
            }
        }
        foreach (var folder in folders)
        {
            DeleteFolderRecursive(folder);
        }
    }

    /// <summary>
    /// Recursively deletes the contents of the folder and the folder itself, provided that there are no files left.
    /// </summary>
    /// <param name="folder">The <see cref="DirectoryInfo" /></param>
    private void DeleteFolderRecursive(DirectoryInfo folder)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            foreach (var subFolder in folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly))
            {
                DeleteFolderRecursive(subFolder);
            }
            if (!folder.GetFiles(ASTERIX, SearchOption.TopDirectoryOnly).Any() && !folder.GetDirectories(ASTERIX, SearchOption.TopDirectoryOnly).Any())
            {
                try
                {
                    folder.Delete();
                    OutputMessage(string.Format(fmtDeletedFolder, folder.FullName));
                }
                catch (IOException ex)
                {
                    OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
                }
                catch (Exception ex)
                {
                    OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
                }
            }
        }
        catch (Exception ex) // should not happen
        {
            OutputMessage(string.Format(fmtErrorFolder, folder.FullName, ex.Message));
        }
    }

    #endregion
}