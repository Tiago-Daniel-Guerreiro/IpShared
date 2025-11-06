using Android = global::Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;
using Splat;

namespace IpShared.Android;

[Activity(
    Name = "com.ipshared.app.MainActivity",
    Label = "IpShared",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]

public class MainActivity : AvaloniaMainActivity<global::IpShared.App>
{
    private const string TAG = "IpShared";
    private const int RequestCameraId = 1001;
    private const int RequestImageCapture = 2001;

    // TaskCompletionSource usada para aguardar o resultado do scanner
    private static TaskCompletionSource<string?>? _scanTcs;

    // Instância corrente para permitir chamadas estáticas
    public static MainActivity? Current { get; private set; }
    
    protected override void OnCreate(global::Android.OS.Bundle savedInstanceState)
    {
        try
        {
            Log.Info(TAG, "MainActivity.OnCreate - Iniciando...");
            base.OnCreate(savedInstanceState);
            Current = this;
            // Solicita permissão de Câmera em tempo de execução se necessário (Android 6+)
            try
            {
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
                {
                    if (CheckSelfPermission(global::Android.Manifest.Permission.Camera) != global::Android.Content.PM.Permission.Granted)
                    {
                        RequestPermissions(new[] { global::Android.Manifest.Permission.Camera }, RequestCameraId);
                        Log.Info(TAG, "Pedido de permissão de câmera efetuado.");
                    }
                }
            }
            catch (Exception exPerm)
            {
                Log.Warn(TAG, $"Falha ao solicitar permissão de câmera: {exPerm.Message}");
            }
            Log.Info(TAG, "MainActivity.OnCreate - Concluído com sucesso");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"ERRO em MainActivity.OnCreate: {ex.Message}");
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            throw;
        }
    }
    
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        try
        {
            Log.Info(TAG, "CustomizeAppBuilder - Configurando Avalonia...");
            var result = base.CustomizeAppBuilder(builder)
                .WithInterFont();

            // Registar implementação do scanner para a camada partilhada
            try
            {
                Locator.CurrentMutable.RegisterConstant(new AndroidPlatformScanner(), typeof(IpShared.Platform.IPlatformScanner));
                Log.Info(TAG, "AndroidPlatformScanner registado em Locator.");
            }
            catch (Exception exReg)
            {
                Log.Warn(TAG, $"Falha ao registar AndroidPlatformScanner: {exReg.Message}");
            }
            // Registar implementação do editor de texto nativo
            try
            {
                Locator.CurrentMutable.RegisterConstant(new AndroidPlatformTextEditor(), typeof(IpShared.Platform.IPlatformTextEditor));
                Log.Info(TAG, "AndroidPlatformTextEditor registado em Locator.");
            }
            catch (Exception exReg2)
            {
                Log.Warn(TAG, $"Falha ao registar AndroidPlatformTextEditor: {exReg2.Message}");
            }
            Log.Info(TAG, "CustomizeAppBuilder - Configuração concluída");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"ERRO em CustomizeAppBuilder: {ex.Message}");
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    protected override void OnPause()
    {
        try
        {
            Log.Info(TAG, "MainActivity.OnPause");
            base.OnPause();
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"ERRO em OnPause: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        try
        {
            Log.Info(TAG, "MainActivity.OnResume");
            base.OnResume();
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"ERRO em OnResume: {ex.Message}");
        }
    }

    /// <summary>
    /// Inicia a intent da câmera para capturar uma imagem (thumbnail) e tenta decodificar um QR Code.
    /// Retorna o texto lido ou null.
    /// </summary>
    public static Task<string?> StartCameraAndDecodeAsync()
    {
        if (Current == null)
            return Task.FromResult<string?>(null);

        try
        {
            _scanTcs = new TaskCompletionSource<string?>();
            var intent = new Intent(MediaStore.ActionImageCapture);
            Current.StartActivityForResult(intent, RequestImageCapture);
            return _scanTcs.Task;
        }
        catch (Exception ex)
        {
            _scanTcs?.TrySetResult(null);
            return Task.FromResult<string?>(null);
        }
    }

    protected override void OnDestroy()
    {
        try
        {
            Log.Info(TAG, "MainActivity.OnDestroy - Limpando recursos...");
            base.OnDestroy();
            if (Current == this) Current = null;
            Log.Info(TAG, "MainActivity.OnDestroy - Concluído");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"ERRO em OnDestroy: {ex.Message}");
        }
    }

    protected override void OnActivityResult(int requestCode, global::Android.App.Result resultCode, Intent data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestImageCapture)
        {
            if (resultCode == global::Android.App.Result.Ok && data != null)
            {
                try
                {
                    var bmp = data.Extras?.Get("data") as global::Android.Graphics.Bitmap;
                    if (bmp != null)
                    {
                        using var ms = new MemoryStream();
                        bmp.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg, 90, ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        using var skbmp = SKBitmap.Decode(ms);
                        if (skbmp != null)
                        {
                            var source = new SKBitmapLuminanceSource(skbmp);
                            var reader = new BarcodeReaderGeneric();
                            var result = reader.Decode(source);
                            if (result != null)
                            {
                                _scanTcs?.TrySetResult(result.Text);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Erro ao processar imagem capturada: {ex.Message}");
                }
            }

            // Se chegou aqui, falhou
            _scanTcs?.TrySetResult(null);
        }
    }
}
