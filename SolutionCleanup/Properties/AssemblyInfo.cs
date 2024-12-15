using SolutionCleanup;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(Vsix.Name)]
[assembly: AssemblyDescription(Vsix.Description)]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(Vsix.Author)]
[assembly: AssemblyProduct(Vsix.Name)]
[assembly: AssemblyCopyright(Vsix.Author)]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyVersion(Vsix.Version)]
[assembly: AssemblyFileVersion(Vsix.Version)]

#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace System.Runtime.CompilerServices
#pragma warning restore IDE0161 // Convert to file-scoped namespace
{
	public class IsExternalInit { }
}