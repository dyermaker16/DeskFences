using System.Windows;

namespace DeskFences;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new FenceWindow().Show();
    }
}
