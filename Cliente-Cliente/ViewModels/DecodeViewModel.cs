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

        var format = InviteGenerator.TryDecodeInvite(InviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            ResultMessage = "Falha ao descodificar: Formato desconhecido ou inválido.";
        }
        else
        {
            ResultMessage = "Convite descodificado com sucesso!";
            DetectedFormat = format.ToString();
            DecodedIp = decodedIpPort.ip.ToString();
            DecodedPort = decodedIpPort.port.ToString();
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
