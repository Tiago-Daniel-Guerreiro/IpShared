using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace IpShared.Views;

public partial class QrCodeWindow : Window
{
    public QrCodeWindow()
    {
        InitializeComponent();
    }

    public QrCodeWindow(Bitmap qrImage) : this()
    {
        QrImage.Source = qrImage;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
