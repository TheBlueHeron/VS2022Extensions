using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SolutionCleanup;

internal partial class OptionsProvider
{
    [ComVisible(true)]
    public class GeneralOptions : BaseOptionPage<General> { }
}

public class General : BaseOptionModel<General>
{
    [Category("General")]
    [DisplayName("Run default cleanup")]
    [Description("Execute the 'Build.CleanSolution' command (i.e. default cleanup)")]
    [DefaultValue(true)]
    public bool RunDefaultCleanup { get; set; } = true;

    [Category("General")]
    [DisplayName("Run on solution close")]
    [Description("Execute cleanup when closing the solution")]
    [DefaultValue(false)]
    public bool RunOnClose { get; set; }

    [Category("General")]
    [DisplayName("Delete bin and obj folder")]
    [Description("Deletes the bin and obj folders unless it is under source control")]
    [DefaultValue(true)]
    public bool DeleteBinFolder { get; set; } = true;

    [Category("General")]
    [DisplayName("Delete packages folder")]
    [Description("Deletes the packages folder unless it is under source control")]
    [DefaultValue(false)]
    public bool DeletePackagesFolder { get; set; } 

    [Category("General")]
    [DisplayName("Delete TestResults folder")]
    [Description("Deletes the TestResults folders on close unless they are under source control")]
    [DefaultValue(false)]
    public bool DeleteTestResultsFolder { get; set; } = false;

    [Category("General")]
    [DisplayName("Delete .vs folder")]
    [Description("Deletes the .vs folders on close unless they are under source control")]
    [DefaultValue(false)]
    public bool DeleteDotVsFolder { get; set; }

    // IIS Express
    [Category("IIS Express")]
    [DisplayName("Delete Logs folder")]
    [Description("Deletes the Logs folders of IIS Express")]
    [DefaultValue(false)]
    public bool DeleteIISExpressLogsFolder { get; set; }

    [Category("IIS Express")]
    [DisplayName("Delete TraceLogFiles folder")]
    [Description("Deletes the TraceLogFiles folders of IIS Express")]
    [DefaultValue(false)]
    public bool DeleteIISExpressTraceLogFilesFolder { get; set; }

    public static explicit operator General(DialogPage v)
    {
        throw new NotImplementedException();
    }
}