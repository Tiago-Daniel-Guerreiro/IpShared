using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using IpShared.ViewModels;
using IpShared.Views;
using AvaloniaApplication = Avalonia.Application;
using System;

namespace IpShared;

public partial class App : AvaloniaApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Suporte para Desktop (Windows, Linux, macOS)
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
        // Suporte para Mobile (Android, iOS)
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("App: Iniciando MainWindowViewModel...");
                var mainViewModel = new MainWindowViewModel();
                System.Diagnostics.Debug.WriteLine("App: MainWindowViewModel criado com sucesso");
                
                var tabControl = new TabControl();
                tabControl.Items.Add(new TabItem { Header = "Codificar", Content = new GenerateView { DataContext = mainViewModel.GenerateVM } });
                tabControl.Items.Add(new TabItem { Header = "Descodificar", Content = new DecodeView { DataContext = mainViewModel.DecodeVM } });
                tabControl.Items.Add(new TabItem { Header = "Conexão", Content = new HostClientView { DataContext = mainViewModel.HostClientVM } });
                singleView.MainView = tabControl;
                System.Diagnostics.Debug.WriteLine("App: TabControl configurado com sucesso");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App: ERRO - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"App: Stack - {ex.StackTrace}");
                
                // Se der erro, mostra mensagem de erro em container simples
                singleView.MainView = new Border
                {
                    Background = Brushes.White,
                    Child = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = $"ERRO na inicialização:\n\n{ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}",
                            FontSize = 12,
                            Foreground = Brushes.Red,
                            Padding = new Thickness(10),
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
