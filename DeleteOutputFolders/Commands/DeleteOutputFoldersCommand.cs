using System.IO;
using System.Linq;
using Microsoft.VisualStudio;

namespace DeleteOutputFolders;

/// <summary>
/// The DeleteOutputFolders command handler.
/// </summary>
[Command(PackageIds.DeleteOutputFoldersCommand)]
internal sealed class DeleteOutputFoldersCommand : BaseCommand<DeleteOutputFoldersCommand>
{
    #region Objects and variables

    private const string ASTERIX = "*";
    private const string BIN = "bin";
    private const string OBJ = "obj";
    private const string STATUS = "Removing bin/obj folders";

    #endregion

    protected async override Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var projects = (await VS.Solutions.GetAllProjectsAsync()).ToList();
        var outputChannel = Package.GetOutputPane(VSConstants.SID_SVsGeneralOutputWindowPane, "Delete bin/obj folders");

        foreach (var p in projects.ToList())
        {
            var idx = 0;
            var directory = new DirectoryInfo(Path.GetDirectoryName(p.FullPath));
            var deletables = directory.GetDirectories(ASTERIX, SearchOption.AllDirectories)
                .Where(d => d.Name.Equals(BIN, StringComparison.InvariantCulture) || d.Name.Equals(OBJ, StringComparison.InvariantCulture)).ToList();

            //await VS.StatusBar.ShowProgressAsync(STATUS, idx, deletables.Count);
            foreach (var d in deletables)
            {
                var fullPath = d.FullName;
                try
                {
                    idx++;
                    d.Delete(true);
                    outputChannel.OutputString($"Deleted folder {d.FullName}.\r\n");
                }
                catch (IOException ex)
                {
                    outputChannel.OutputString($"Error deleting folder {d.FullName}. {ex.Message}\r\n");
                }
                finally
                {
                    //await VS.StatusBar.ShowProgressAsync(STATUS, idx, deletables.Count);
                }
            };
        };
    }
}
