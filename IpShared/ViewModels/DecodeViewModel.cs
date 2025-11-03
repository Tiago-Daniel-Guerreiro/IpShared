using Invite_Generator.Refactored;
using ReactiveUI;
using System;

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
                DetectedFormat = format.ToString();
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
            {
                ResultMessage += $"Detalhes: {ex.InnerException.Message}";
            }
#else
            // Em modo Release, mostra apenas mensagem genérica
            ResultMessage = "Erro ao descodificar o convite. Verifique se o código está correto.";
#endif
        }
    }

    private void ClearResults()
    {
        ResultMessage = string.Empty;
        DetectedFormat = string.Empty;
        DecodedIp = string.Empty;
        DecodedPort = string.Empty;
    }
}
