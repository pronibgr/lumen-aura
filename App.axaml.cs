using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using aura.ViewModels;
using aura.Views;

namespace aura;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
#if !ANDROID
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };

            try
            {
                string iconPng = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "aura.png");
                if (System.IO.File.Exists(iconPng))
                {
                    using var stream = System.IO.File.OpenRead(iconPng);
                    mainWindow.Icon = new Avalonia.Controls.WindowIcon(new Avalonia.Media.Imaging.Bitmap(stream));
                }
                else if (System.IO.File.Exists("Assets/aura.png"))
                {
                    using var stream = System.IO.File.OpenRead("Assets/aura.png");
                    mainWindow.Icon = new Avalonia.Controls.WindowIcon(new Avalonia.Media.Imaging.Bitmap(stream));
                }
            }
            catch { }

            desktop.MainWindow = mainWindow;

            // Initialize System Tray Icon & Menu
            Services.TrayService.Instance.Initialize(mainWindow);

            desktop.Exit += (s, e) =>
            {
                Services.VpnEngineService.Instance.Disconnect();
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Services.VpnEngineService.Instance.Disconnect();
            };
        }
        else
#endif
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainAdaptiveView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}