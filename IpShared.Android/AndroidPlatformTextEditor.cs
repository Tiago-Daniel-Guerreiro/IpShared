using System;
using System.Threading.Tasks;
using Android.App;
using Android.Widget;
// Qualificamos explicitamente Android.Text via global::Android.Text para evitar ambiguidade com o namespace IpShared.Android
using IpShared.Platform;

namespace IpShared.Android
{
    public class AndroidPlatformTextEditor : IPlatformTextEditor
    {
        public Task<string?> EditTextAsync(string initialText, bool readOnly = false)
        {
            var tcs = new TaskCompletionSource<string?>();

            var activity = MainActivity.Current;
            if (activity == null)
            {
                tcs.SetResult(null);
                return tcs.Task;
            }

            activity.RunOnUiThread(() =>
            {
                try
                {
                    var builder = new AlertDialog.Builder(activity);
                    var edit = new EditText(activity)
                    {
                        Text = initialText ?? string.Empty,
                    };
                    // Posiciona o cursor no final por padrão
                    edit.SetSelection(edit.Text?.Length ?? 0);

                    if (readOnly)
                    {
                        // Permite seleção nativa em Android sem permitir edição
                        edit.Focusable = true;
                        edit.FocusableInTouchMode = true;
                        edit.LongClickable = true;
                        // Habilita seleção de texto (mostra handles) e desativa teclado
                        edit.SetTextIsSelectable(true);
                        edit.InputType = global::Android.Text.InputTypes.Null;
                        // Remove KeyListener para prevenir entrada de texto
                        edit.KeyListener = null;
                    }

                    builder.SetView(edit);
                    builder.SetPositiveButton("OK", (sender, args) =>
                    {
                        tcs.TrySetResult(edit.Text);
                    });
                    builder.SetNegativeButton("Cancelar", (sender, args) =>
                    {
                        tcs.TrySetResult(null);
                    });

                    var dialog = builder.Create();
                    dialog.Show();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}
