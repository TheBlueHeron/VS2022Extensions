using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SolutionCleanup;

/// <summary>
/// Provider for the <see cref="BaseOptionPage{General}"/> page.
/// </summary>
internal partial class OptionsProvider
{
    [ComVisible(true)]
    public class GeneralOptions : BaseOptionPage<General> { }
}

/// <summary>
/// <see cref="BaseOptionModel{General}"/> for the settings, provided under 'Tools -> Options -> Environment -> Solution Cleanup'.
/// </summary>
public class General : BaseOptionModel<General>
{
    [Category("General")]
    [DisplayName("Run default cleanup")]
    [Description("Execute the default 'Clean Solution' command.")]
    [DefaultValue(true)]
    public bool RunDefaultCleanup { get; set; } = true;

    [Category("General")]
    [DisplayName("Delete bin and obj folder")]
    [Description("Delete the bin and obj folders unless they are under source control.")]
    [DefaultValue(true)]
    public bool DeleteBinFolder { get; set; } = true;

    [Category("General")]
    [DisplayName("Delete packages folder")]
    [Description("Delete the packages folder unless it is under source control.")]
    [DefaultValue(false)]
    public bool DeletePackagesFolder { get; set; } 

    [Category("General")]
    [DisplayName("Delete TestResults folder")]
    [Description("Delete the TestResults folders unless they are under source control.")]
    [DefaultValue(false)]
    public bool DeleteTestResultsFolder { get; set; } = false;

    [Category("General")]
    [DisplayName("Delete .vs folder")]
    [Description("Delete the .vs folders unless they are under source control.")]
    [DefaultValue(false)]
    public bool DeleteDotVsFolder { get; set; }

    // IIS Express
    [Category("IIS Express")]
    [DisplayName("Delete Logs folder")]
    [Description("Delete the Logs folders of IIS Express.")]
    [DefaultValue(false)]
    public bool DeleteIISExpressLogsFolder { get; set; }

    [Category("IIS Express")]
    [DisplayName("Delete TraceLogFiles folder")]
    [Description("Delete the TraceLogFiles folders of IIS Express.")]
    [DefaultValue(false)]
    public bool DeleteIISExpressTraceLogFilesFolder { get; set; }

    public static explicit operator General(DialogPage v)
    {
        throw new NotImplementedException();
    }
}