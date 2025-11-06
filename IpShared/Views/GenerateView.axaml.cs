using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace IpShared.Views;

public partial class GenerateView : UserControl
{
    public GenerateView()
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

    private async void OnCopyQrClicked(object? sender, RoutedEventArgs e)
    {
        var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
        var clipboard = top?.Clipboard;
        var vm = DataContext as ViewModels.GenerateViewModel;
        if (clipboard != null && vm?.QrCodeBase64 != null)
        {
            await clipboard.SetTextAsync(vm.QrCodeBase64).ConfigureAwait(false);
            vm.StatusMessage = "Base64 do QR Code copiado para a área de transferência!";
        }
    }
    // Forwarder: delega a lógica real para o handler centralizado TextBoxAutoHandler
    private async void OnTextBoxAuto(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBox tb)
            await TextBoxAutoHandler.HandleAsync(this, tb, e).ConfigureAwait(false);
    }
}
