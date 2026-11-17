using Artbrewify.Core;
using Microsoft.UI.Xaml;

namespace Artbrewify;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static SettingsStore Settings { get; } = new();

    public App()
    {
        InitializeComponent();
        Settings.Load();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}