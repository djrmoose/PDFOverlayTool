using System.Windows;

namespace PdfOverlayTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        TelemetryCrashHandler.Initialize();
        base.OnStartup(e);
    }
}

