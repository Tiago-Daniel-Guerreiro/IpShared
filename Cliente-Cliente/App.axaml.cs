using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IpShared.ViewModels;
using IpShared.Views;

namespace IpShared;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Garante que o recurso CancellationTokenSource é libertado ao fechar
            desktop.ShutdownRequested += (sender, args) =>
            {
                mainViewModel.HostClientVM.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
