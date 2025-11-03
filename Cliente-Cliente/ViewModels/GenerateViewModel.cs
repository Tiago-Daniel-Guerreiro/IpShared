using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Invite_Generator.Refactored;
using ReactiveUI;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace IpShared.ViewModels;

public class GenerateViewModel : ViewModelBase
{
    private string _port = "60000";
    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private bool _usePublicIp = true;
    public bool UsePublicIp
    {
        get => _usePublicIp;
        set => this.RaiseAndSetIfChanged(ref _usePublicIp, value);
    }

    private string _statusMessage = "Pronto para gerar convites.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    // Propriedades para exibir os convites gerados
    private string? _defaultInvite;
    public string? DefaultInvite { get => _defaultInvite; set => this.RaiseAndSetIfChanged(ref _defaultInvite, value); }
    
    private string? _base16Invite;
    public string? Base16Invite { get => _base16Invite; set => this.RaiseAndSetIfChanged(ref _base16Invite, value); }

    private string? _base62Invite;
    public string? Base62Invite { get => _base62Invite; set => this.RaiseAndSetIfChanged(ref _base62Invite, value); }

    private string? _humanInvite;
    public string? HumanInvite { get => _humanInvite; set => this.RaiseAndSetIfChanged(ref _humanInvite, value); }

    private Bitmap? _qrCodeImage;
    public Bitmap? QrCodeImage { get => _qrCodeImage; set => this.RaiseAndSetIfChanged(ref _qrCodeImage, value); }
    
    public async Task GenerateInvitesAsync()
    {
        ClearResults();
        StatusMessage = "A gerar convites, por favor aguarde...";

        if (!ushort.TryParse(Port, out var portNumber))
        {
            StatusMessage = "Erro: A porta inserida é inválida.";
            return;
        }

        try
        {
            InviteGenerator generator;
            if (UsePublicIp)
            {
                StatusMessage = "A obter IP Público via STUN...";
                generator = await InviteGenerator.CreateAsync(portNumber);
            }
            else
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, portNumber);
            }

            // Atualiza as propriedades com os convites
            DefaultInvite = generator.ObterConvite(InviteFormat.Default);
            Base16Invite = generator.ObterConvite(InviteFormat.Base16);
            Base62Invite = generator.ObterConvite(InviteFormat.Base62);

            try { HumanInvite = generator.ObterConvite(InviteFormat.Human); }
            catch (Exception ex) { HumanInvite = $"ERRO: {ex.Message}"; }
            
            // Converte o Base64 do QR Code para uma imagem
            var base64Qr = generator.ObterConvite(InviteFormat.QrCodeBase64);
            var imageBytes = Convert.FromBase64String(base64Qr);
            using (var ms = new MemoryStream(imageBytes))
            {
                QrCodeImage = new Bitmap(ms);
            }
            
            StatusMessage = "Convites gerados com sucesso!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ocorreu um erro: {ex.Message}";
            ClearResults();
        }
    }

    private void ClearResults()
    {
        DefaultInvite = string.Empty;
        Base16Invite = string.Empty;
        Base62Invite = string.Empty;
        HumanInvite = string.Empty;
        QrCodeImage = null;
    }
}
