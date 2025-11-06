using Invite_Generator;
using ReactiveUI;
using System;
using System.Threading.Tasks;

namespace IpShared.ViewModels;

public class DecodeViewModel : ViewModelBase
{
    private string? _inviteCode;
    public string? InviteCode
    {
        get => _inviteCode;
        set => this.RaiseAndSetIfChanged(ref _inviteCode, value);
    }

    private string? _resultMessage;
    public string? ResultMessage
    {
        get => _resultMessage;
        set => this.RaiseAndSetIfChanged(ref _resultMessage, value);
    }
    
    private string? _decodedIp;
    public string? DecodedIp { get => _decodedIp; set => this.RaiseAndSetIfChanged(ref _decodedIp, value); }
    
    private string? _decodedPort;
    public string? DecodedPort { get => _decodedPort; set => this.RaiseAndSetIfChanged(ref _decodedPort, value); }
    
    private string? _detectedFormat;
    public string? DetectedFormat { get => _detectedFormat; set => this.RaiseAndSetIfChanged(ref _detectedFormat, value); }

    // Detecta se está rodando em mobile (Android/iOS)
    public bool IsMobile
    {
        get
        {
#if ANDROID || IOS
            return true;
#else
            return false;
#endif
        }
    }

    public void DecodeInvite()
    {
        ClearResults();
        
        if (string.IsNullOrWhiteSpace(InviteCode))
        {
            ResultMessage = "Por favor, insira um código de convite.";
            return;
        }

        var code = InviteCode.Trim();
        
        try
        {
            // O InviteGenerator.TryDecodeInvite já cuida de detectar e decodificar QR Code Base64
            var format = InviteGenerator.TryDecodeInvite(code, out var decodedIpPort);

            if (format == InviteFormat.Unknown)
            {
                ResultMessage = "Falha ao descodificar: Formato desconhecido ou inválido.\n\n";
                ResultMessage += "Formatos suportados:\n";
                ResultMessage += "• Default: IP:Porta (ex: 192.168.1.1:50000)\n";
                ResultMessage += "• Base16: Hexadecimal de 12 caracteres\n";
                ResultMessage += "• Base62: Código alfanumérico\n";
                ResultMessage += "• Human: 5 palavras separadas por hífen\n";
                ResultMessage += "• QR Code: Base64 de imagem PNG";
            }
            else
            {
                ResultMessage = "Convite descodificado com sucesso!";
                DetectedFormat = format == InviteFormat.Human ? "Words" : format.ToString();
                DecodedIp = decodedIpPort.ip.ToString();
                DecodedPort = decodedIpPort.port.ToString();
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            // Em modo DEBUG, mostra detalhes completos do erro
            ResultMessage = $"Erro ao descodificar: {ex.Message}\n\n";
            ResultMessage += $"Tipo de erro: {ex.GetType().Name}\n";
            if (ex.InnerException != null)
                ResultMessage += $"Detalhes: {ex.InnerException.Message}";
#else
            // Em modo Release, mostra apenas mensagem genérica
            _ = ex; // Suprime aviso CS0168
            ResultMessage = "Erro ao descodificar o convite. Verifique se o código está correto.";
#endif
        }
    }

    public async Task ScanQrCode()
    {
#if ANDROID
        ResultMessage = "Abrindo câmera para escanear QR Code...";
        try
        {
            // Tenta resolver o serviço de plataforma (implementado no projeto Android)
            try
            {
                var resolver = Splat.Locator.Current;
                var svc = resolver.GetService(typeof(IpShared.Platform.IPlatformScanner));
                if (svc is IpShared.Platform.IPlatformScanner scanner)
                {
                    var scanned = await scanner.ScanAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(scanned))
                    {
                        // Decodifica o invite diretamente se for o conteúdo do QR
                        InviteCode = scanned;
                        ResultMessage = "QR Code capturado. A tentar descodificar...";
                        await Task.Delay(50).ConfigureAwait(false);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => DecodeInvite());
                        return;
                    }
                    else
                    {
                        ResultMessage = "Nenhum QR Code foi detetado na imagem capturada.";
                        return;
                    }
                }
                else
                {
                    ResultMessage = "Scanner não disponível nesta plataforma. Verifique se o aplicativo foi construído para Android.";
                    return;
                }
            }
            catch (System.Exception ex)
            {
                ResultMessage = $"Erro ao tentar usar a câmera: {ex.Message}";
                return;
            }
            
        }
        catch (System.Exception ex)
        {
            ResultMessage = $"Erro ao tentar usar a câmera: {ex.Message}";
        }
#else
        await Task.CompletedTask;
        ResultMessage = "Scan de QR Code disponível apenas em dispositivos móveis.";
#endif
    }

    private void ClearResults()
    {
        ResultMessage = string.Empty;
        DetectedFormat = string.Empty;
        DecodedIp = string.Empty;
        DecodedPort = string.Empty;
    }
}
