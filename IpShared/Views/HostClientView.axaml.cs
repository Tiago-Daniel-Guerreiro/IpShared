using Avalonia.Controls;
using Avalonia.Input;
using IpShared.ViewModels;

namespace IpShared.Views;

public partial class HostClientView : UserControl
{
    public HostClientView()
    {
        InitializeComponent();
    }

    private void OnInputGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Text = string.Empty;
        }
    }

    private async void OnLogTextBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is HostClientViewModel vm)
        {
            var text = vm.ClientLog ?? string.Empty;
            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
            var clipboard = top?.Clipboard;
            if (!string.IsNullOrEmpty(text) && clipboard != null)
            {
                await clipboard.SetTextAsync(text).ConfigureAwait(false);
                vm.ClientLog += "[Info] Registo copiado para a área de transferência.\n";
            }
        }
    }
    
    // Forwarder: delega a lógica real para o handler centralizado TextBoxAutoHandler
    private async void OnTextBoxAuto(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBox tb)
            await TextBoxAutoHandler.HandleAsync(this, tb, e).ConfigureAwait(false);
    }

    
}
