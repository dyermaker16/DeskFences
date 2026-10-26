using System.Windows;
using DeskFences.Models;

namespace DeskFences;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new FenceWindow(new FenceModel { Title = "Games" }).Show();
    }
}
