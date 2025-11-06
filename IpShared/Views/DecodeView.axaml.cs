using Avalonia.Controls;
using Avalonia.Input;

namespace IpShared.Views;

public partial class DecodeView : UserControl
{
    public DecodeView()
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

    // Forwarder: delega a lógica real para o handler centralizado TextBoxAutoHandler
    private async void OnTextBoxAuto(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBox tb)
            await TextBoxAutoHandler.HandleAsync(this, tb, e).ConfigureAwait(false);
    }
}
