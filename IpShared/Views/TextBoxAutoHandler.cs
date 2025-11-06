using Avalonia.Controls;
using Avalonia.Input;
using Splat;
using System.Threading.Tasks;

namespace IpShared.Views
{
    /// <summary>
    /// Handler centralizado para o comportamento "touch opens native editor / fallback copy or focus".
    /// Chamado pelos code-behinds das Views para evitar duplicação.
    /// </summary>
    public static class TextBoxAutoHandler
    {
        public static async Task HandleAsync(UserControl owner, TextBox tb, PointerPressedEventArgs e)
        {
            if (tb == null)
                return;

            try
            {
                var resolver = Locator.Current;
                var svc = resolver.GetService(typeof(IpShared.Platform.IPlatformTextEditor));
                if (svc is IpShared.Platform.IPlatformTextEditor editor)
                {
                    var initial = tb.Text ?? string.Empty;
                    var edited = await editor.EditTextAsync(initial, readOnly: tb.IsReadOnly).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(edited) && !tb.IsReadOnly)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => tb.Text = edited);
                    }
                    e.Handled = true;
                    return;
                }
            }
            catch
            {
                // Swallow - non-critical UX helper
            }

            // Fallback: if readonly, copy to clipboard; otherwise set focus
            if (tb.IsReadOnly)
            {
                var top = Avalonia.Controls.TopLevel.GetTopLevel(owner);
                var clipboard = top?.Clipboard;
                if (!string.IsNullOrEmpty(tb.Text) && clipboard != null)
                {
                    await clipboard.SetTextAsync(tb.Text).ConfigureAwait(false);
                }
            }
            else
            {
                tb.Focus();
            }
        }
    }
}
