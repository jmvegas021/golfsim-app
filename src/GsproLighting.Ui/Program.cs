using GsproLighting.Core.Config;
using GsproLighting.Ui.Hosting;

namespace GsproLighting.Ui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        var store = new ConfigStore();
        var coordinator = new LightingAppCoordinator(store);
        Application.Run(new TrayApplicationContext(coordinator));
    }
}
