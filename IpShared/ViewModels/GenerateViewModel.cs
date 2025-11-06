using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Invite_Generator;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace IpShared.ViewModels;

public class GenerateViewModel : ViewModelBase
{
    private string _port = "8000";
    public string Port
    {
        get => _port;
        set
        {
            // Validação: apenas números e entre 0-65535
            if (string.IsNullOrEmpty(value) || (ushort.TryParse(value, out var p) && p <= 65535))
                this.RaiseAndSetIfChanged(ref _port, value);
        }
    }

    private bool _usePublicIp = true;
    public bool UsePublicIp
    {
        get => _usePublicIp;
        set
        {
            this.RaiseAndSetIfChanged(ref _usePublicIp, value);
            // Quando ativamos IP público, desativamos automaticamente a opção de IP local
            if (value)
                UseLocalIp = false;
            // Notifica mudança na disponibilidade das opções locais
            this.RaisePropertyChanged(nameof(LocalOptionsEnabled));
        }
    }

    // Helper para ativar/desativar opções locais quando UsePublicIp está ativo
    public bool LocalOptionsEnabled => !_usePublicIp;

    // Detecta se está rodando em mobile (Android/iOS) para a UI poder mostrar opções nativas
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

    private string _fixedIp = "127.0.0.1";
    public string FixedIp
    {
        get => _fixedIp;
        set
        {
            // Validação: formato IP válido
            if (string.IsNullOrEmpty(value) || IsValidIpFormat(value))
                this.RaiseAndSetIfChanged(ref _fixedIp, value);
        }
    }

    private bool _useLocalIp = false;
    public bool UseLocalIp
    {
        get => _useLocalIp;
        set
        {
            this.RaiseAndSetIfChanged(ref _useLocalIp, value);
            if (value)
                LoadLocalIp();
            else
                FixedIp = "127.0.0.1"; // Ao desativar, voltar para 127.0.0.1 (editável)
        }
    }

    private int _dictionaryId = 0;
    public int DictionaryId
    {
        get => _dictionaryId;
        set
        {
            if (value >= 0 && value <= 15)
                this.RaiseAndSetIfChanged(ref _dictionaryId, value);
        }
    }

    private string _dictionaryIdText = "0";
    public string DictionaryIdText
    {
        get => _dictionaryIdText;
        set
        {
            // Bloqueia entrada de não-números
            if (!string.IsNullOrEmpty(value) && !int.TryParse(value, out _))
                return;
            
            // Bloqueia valores maiores que 15
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int num) && num > 15)
                return;
            
            this.RaiseAndSetIfChanged(ref _dictionaryIdText, value);
            
            // Atualiza o DictionaryId apenas se for um número válido
            if (string.IsNullOrWhiteSpace(value))
                _dictionaryId = 0; // Default para 0 quando vazio
            else if (int.TryParse(value, out var id) && id >= 0 && id <= 15)
                _dictionaryId = id;
        }
    }

    private string _statusMessage = "Pronto para codificar convites.";
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

    private string? _qrCodeBase64;
    public string? QrCodeBase64 { get => _qrCodeBase64; set => this.RaiseAndSetIfChanged(ref _qrCodeBase64, value); }
    
    public async Task GenerateInvitesAsync()
    {
        ClearResults();
            StatusMessage = "A codificar convites, por favor aguarde...";

        if (!ushort.TryParse(Port, out var portNumber))
        {
            StatusMessage = "Erro: A porta inserida é inválida (deve estar entre 0 e 65535).";
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
            else if (UseLocalIp)
            {
                var localIp = NetworkHelper.GetActiveIPv4Address();
                if (localIp == null)
                {
                    StatusMessage = "Erro: Não foi possível obter o IP local.";
                    return;
                }
                generator = InviteGenerator.CreateWithFixedIp(localIp, portNumber);
                StatusMessage = $"Usando IP local: {localIp}";
            }
            else
            {
                if (!IPAddress.TryParse(FixedIp, out var ipAddress))
                {
                    StatusMessage = "Erro: O endereço IP inserido é inválido.";
                    return;
                }
                generator = InviteGenerator.CreateWithFixedIp(ipAddress, portNumber);
            }

            // Atualiza as propriedades com os convites
            DefaultInvite = generator.ObterConvite(InviteFormat.Default);
            Base16Invite = generator.ObterConvite(InviteFormat.Base16);
            Base62Invite = generator.ObterConvite(InviteFormat.Base62);

            try { HumanInvite = generator.ObterConvite(InviteFormat.Human, DictionaryId); }
            catch (Exception ex) { HumanInvite = $"ERRO: {ex.Message}"; }
            
            // Converte o Base64 do QR Code para uma imagem
            var base64Qr = generator.ObterConvite(InviteFormat.QrCodeBase64);
            QrCodeBase64 = base64Qr;
            var imageBytes = Convert.FromBase64String(base64Qr);
            using (var ms = new MemoryStream(imageBytes))
            {
                QrCodeImage = Bitmap.DecodeToWidth(ms, 300);
            }
            
            StatusMessage = "Convites gerados com sucesso!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ocorreu um erro: {ex.Message}";
            ClearResults();
        }
    }

    private bool IsValidIpFormat(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length > 4) 
            return false;
        
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) 
                continue;
            if (!byte.TryParse(part, out var num)) 
                return false;
            if (num > 255) 
                return false;
        }
        
        return true;
    }

    private void LoadLocalIp()
    {
        var localIp = NetworkHelper.GetActiveIPv4Address();
        if (localIp != null)
            FixedIp = localIp.ToString();
    }

    private void ClearResults()
    {
        DefaultInvite = string.Empty;
        Base16Invite = string.Empty;
        Base62Invite = string.Empty;
        HumanInvite = string.Empty;
        QrCodeImage = null;
        QrCodeBase64 = string.Empty;
    }

    public void CopyQrCodeBase64()
    {
        if (!string.IsNullOrEmpty(QrCodeBase64))
        {
            try
            {
                // Copia para a área de transferência usando TopLevel
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel?.Clipboard != null)
                {
                    _ = topLevel.Clipboard.SetTextAsync(QrCodeBase64);
                    StatusMessage = "Base64 do QR Code copiado para a área de transferência!";
                }
                else
                {
                    StatusMessage = "Não foi possível acessar a área de transferência.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao copiar: {ex.Message}";
            }
        }
    }
}
