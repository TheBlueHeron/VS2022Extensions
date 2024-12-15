global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Threading;

namespace SolutionCleanup;

/// <summary>
/// The package definition.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
[ProvideAutoLoad(UIContextGuids.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(UIContextGuids.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Environment", Vsix.Name, 101, 102, true, [], SupportsProfiles = true, ProvidesLocalizedCategoryName = false)]
[ProvideProfile(typeof(OptionsProvider.GeneralOptions), "Environment", Vsix.Name, 101, 102, true)]
[Guid(PackageGuids.SolutionCleanupString)]
public sealed class SolutionCleanupPackage : ToolkitPackage
{
	protected async override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
	{
		await this.RegisterCommandsAsync();
    }
}