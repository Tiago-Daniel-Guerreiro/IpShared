using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Invite_Generator.Refactored;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpShared.ViewModels;

public class HostClientViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cts;

    public HostClientViewModel()
    {
        // Inicializa com IP local se possível
        LoadLocalIp();
    }

    // Propriedades do Host
    private bool _useFixedIp = true;
    public bool UseFixedIp { get => _useFixedIp; set => this.RaiseAndSetIfChanged(ref _useFixedIp, value); }

    private bool _useLocalIp = true;
    public bool UseLocalIp
    {
        get => _useLocalIp;
        set
        {
            this.RaiseAndSetIfChanged(ref _useLocalIp, value);
            if (value && UseFixedIp)
            {
                LoadLocalIp();
            }
        }
    }

    private string _hostIp = "127.0.0.1";
    public string HostIp
    {
        get => _hostIp;
        set
        {
            if (string.IsNullOrEmpty(value) || IsValidIpFormat(value))
            {
                this.RaiseAndSetIfChanged(ref _hostIp, value);
            }
        }
    }

    private bool _isHostRunning = false;
    public bool IsHostRunning { get => _isHostRunning; set => this.RaiseAndSetIfChanged(ref _isHostRunning, value); }

    private string _hostPort = "60000";
    public string HostPort
    {
        get => _hostPort;
        set
        {
            // Validação: apenas números e entre 0-65535
            if (string.IsNullOrEmpty(value) || (ushort.TryParse(value, out var p) && p <= 65535))
            {
                this.RaiseAndSetIfChanged(ref _hostPort, value);
            }
        }
    }

    private string? _hostInviteCode;
    public string? HostInviteCode { get => _hostInviteCode; set => this.RaiseAndSetIfChanged(ref _hostInviteCode, value); }

    private string? _defaultInvite;
    public string? DefaultInvite { get => _defaultInvite; set => this.RaiseAndSetIfChanged(ref _defaultInvite, value); }

    private string? _base16Invite;
    public string? Base16Invite { get => _base16Invite; set => this.RaiseAndSetIfChanged(ref _base16Invite, value); }

    private string? _base62Invite;
    public string? Base62Invite { get => _base62Invite; set => this.RaiseAndSetIfChanged(ref _base62Invite, value); }

    private string? _humanInvite;
    public string? HumanInvite { get => _humanInvite; set => this.RaiseAndSetIfChanged(ref _humanInvite, value); }

    private string? _qrCodeBase64;
    public string? QrCodeBase64 { get => _qrCodeBase64; set => this.RaiseAndSetIfChanged(ref _qrCodeBase64, value); }

    // Formato selecionado
    // Formato selecionado
    private string _selectedFormat = "Base";
    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedFormat, value);
            UpdateDisplayedInvite();
        }
    }

    // Lista de formatos disponíveis
    public List<string> AvailableFormats { get; } = new List<string>
    {
        "Base",
        "Words",
        "Curto",
        "Muito curto",
        "QR Code"
    };

    // Convite exibido baseado na seleção
    private string? _displayedInvite;
    public string? DisplayedInvite { get => _displayedInvite; set => this.RaiseAndSetIfChanged(ref _displayedInvite, value); }

    // Imagem do QR Code
    private Bitmap? _qrCodeImage;
    public Bitmap? QrCodeImage { get => _qrCodeImage; set => this.RaiseAndSetIfChanged(ref _qrCodeImage, value); }

    // Flag para mostrar se é QR Code
    public bool IsQrCodeFormat => SelectedFormat == "QR Code";

    private string _hostLog = "O servidor está parado.";
    public string HostLog { get => _hostLog; set => this.RaiseAndSetIfChanged(ref _hostLog, value); }
    
    // Propriedades do Cliente
    private string? _clientInviteCode;
    public string? ClientInviteCode { get => _clientInviteCode; set => this.RaiseAndSetIfChanged(ref _clientInviteCode, value); }

    private string _clientLog = "Pronto para conectar.";
    public string ClientLog { get => _clientLog; set => this.RaiseAndSetIfChanged(ref _clientLog, value); }

    private bool _isConnecting = false;
    public bool IsConnecting { get => _isConnecting; set => this.RaiseAndSetIfChanged(ref _isConnecting, value); }

    private bool _useCustomMessage = false;
    public bool UseCustomMessage { get => _useCustomMessage; set => this.RaiseAndSetIfChanged(ref _useCustomMessage, value); }

    private string _customMessage = "Olá do cliente!";
    public string CustomMessage { get => _customMessage; set => this.RaiseAndSetIfChanged(ref _customMessage, value); }

    private bool _specifyFormat = false;
    public bool SpecifyFormat { get => _specifyFormat; set => this.RaiseAndSetIfChanged(ref _specifyFormat, value); }

    // Atualiza o convite exibido baseado no formato selecionado
    private void UpdateDisplayedInvite()
    {
        DisplayedInvite = SelectedFormat switch
        {
            "Base" => DefaultInvite,
            "Curto" => Base16Invite,
            "Muito curto" => Base62Invite,
            "Words" => HumanInvite,
            "QR Code" => QrCodeBase64,
            _ => DefaultInvite
        };
        
        // Notifica mudança do IsQrCodeFormat
        this.RaisePropertyChanged(nameof(IsQrCodeFormat));
    }

    // Copia o Base64 do convite atual
    public void CopyBase64()
    {
        if (!string.IsNullOrEmpty(DisplayedInvite))
        {
            try
            {
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel?.Clipboard != null)
                {
                    _ = topLevel.Clipboard.SetTextAsync(DisplayedInvite);
                    HostLog += $"[Sucesso] Base64 copiado para área de transferência.\n";
                }
            }
            catch (Exception ex)
            {
                HostLog += $"[Erro] Falha ao copiar: {ex.Message}\n";
            }
        }
    }

    // Mostra a imagem do QR Code
    public void ShowQrCodeImage()
    {
        if (!string.IsNullOrEmpty(QrCodeBase64) && SelectedFormat == "QR Code")
        {
            try
            {
                var imageBytes = Convert.FromBase64String(QrCodeBase64);
                using (var ms = new MemoryStream(imageBytes))
                {
                    var qrImage = new Bitmap(ms);
                    
                    // Abre janela modal com o QR Code
                    var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    if (mainWindow != null)
                    {
                        var qrWindow = new Views.QrCodeWindow(qrImage);
                        _ = qrWindow.ShowDialog(mainWindow);
                        HostLog += "[Sucesso] Imagem do QR Code aberta.\n";
                    }
                }
            }
            catch (Exception ex)
            {
                HostLog += $"[Erro] Falha ao carregar imagem: {ex.Message}\n";
            }
        }
    }

    public async Task StartHostAsync()
    {
        if (IsHostRunning) return;
        
        IsHostRunning = true;
        _cts = new CancellationTokenSource();
        
        // Valida a porta
        if (!ushort.TryParse(HostPort, out var port))
        {
            HostLog = "Erro: A porta inserida é inválida (deve estar entre 0 e 65535).\n";
            StopHost();
            return;
        }
        
        HostLog = "[Fase 1/3] A gerar convites...\n";

        try
        {
            InviteGenerator generator;
            if (UseFixedIp)
            {
                IPAddress ipAddress;
                if (UseLocalIp)
                {
                    ipAddress = GetLocalIpAddress() ?? IPAddress.Loopback;
                    HostLog += $"Modo IP local: Usando IP {ipAddress}.\n";
                }
                else
                {
                    if (!IPAddress.TryParse(HostIp, out ipAddress))
                    {
                        HostLog += "Erro: O endereço IP inserido é inválido.\n";
                        StopHost();
                        return;
                    }
                    HostLog += $"Modo IP fixo: Usando IP {HostIp}.\n";
                }
                generator = InviteGenerator.CreateWithFixedIp(ipAddress, port);
            }
            else
            {
                generator = await InviteGenerator.CreateAsync(port);
                HostLog += "IP Público obtido via STUN.\n";
            }
            
            // Gera todos os formatos de convite
            DefaultInvite = generator.ObterConvite(InviteFormat.Default);
            Base16Invite = generator.ObterConvite(InviteFormat.Base16);
            Base62Invite = generator.ObterConvite(InviteFormat.Base62);
            HumanInvite = generator.ObterConvite(InviteFormat.Human);
            QrCodeBase64 = generator.ObterConvite(InviteFormat.QrCodeBase64);
            
            // Atualiza o convite exibido
            UpdateDisplayedInvite();
            
            // Mantém compatibilidade com o campo antigo
            HostInviteCode = $"Humano: {HumanInvite}\nDefault: {DefaultInvite}";
            
            HostLog += "[Fase 2/3] Convites gerados em todos os formatos.\n";
            
            // Mostra o IP real usado no servidor
            string displayIp = UseFixedIp 
                ? (UseLocalIp ? GetLocalIpAddress()?.ToString() ?? "0.0.0.0" : HostIp)
                : DefaultInvite.Split(':')[0];
            
            HostLog += $"[Fase 3/3] Servidor ativo em {displayIp}:{port}. A aguardar conexões...\n";
            
            // Inicia o listener numa thread separada para não bloquear a UI
            _ = Task.Run(() => StartTcpListener(port, _cts.Token));
        }
        catch (Exception ex)
        {
            HostLog += $"Erro Crítico: {ex.Message}\n";
            StopHost();
        }
    }

    public void StopHost()
    {
        if (!IsHostRunning) return;

        _cts?.Cancel();
        IsHostRunning = false;
        HostLog += "Servidor a encerrar...\n";
        HostInviteCode = string.Empty;
    }

    private async Task StartTcpListener(ushort port, CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        try
        {
            listener.Start();
            while (!token.IsCancellationRequested)
            {
                if (await listener.AcceptTcpClientAsync(token) is { } client)
                {
                    using (client)
                    using (var streamReader = new StreamReader(client.GetStream()))
                    {
                        string message = await streamReader.ReadToEndAsync(token);
                        // Para atualizar a UI a partir de outra thread, usamos o Dispatcher
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            HostLog += $"[Servidor] Requisição de {client.Client.RemoteEndPoint}: \"{message.Trim()}\"\n";
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* Ignora, é o encerramento normal */ }
        catch (Exception ex) { await Dispatcher.UIThread.InvokeAsync(() => HostLog += $"Erro no listener: {ex.Message}\n"); }
        finally
        {
            listener.Stop();
            await Dispatcher.UIThread.InvokeAsync(() => HostLog += "Servidor encerrado.\n");
        }
    }

    public async Task ConnectClientAsync()
    {
        if (IsConnecting) return;
        
        IsConnecting = true;
        ClientLog = "A iniciar conexão...\n";

        if (string.IsNullOrWhiteSpace(ClientInviteCode))
        {
            ClientLog += "Código de convite inválido.\n";
            IsConnecting = false;
            return;
        }

        var format = InviteGenerator.TryDecodeInvite(ClientInviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            ClientLog += "Falha ao descodificar o código.\n";
            IsConnecting = false;
            return;
        }

        string formatName = format switch
        {
            InviteFormat.Default => "Base",
            InviteFormat.Base16 => "Curto",
            InviteFormat.Base62 => "Muito curto",
            InviteFormat.Human => "Words",
            InviteFormat.QrCodeBase64 => "QR Code",
            _ => "Desconhecido"
        };

        if (SpecifyFormat)
        {
            ClientLog += $"Formato detectado: {formatName}\n";
        }

        ClientLog += $"Código descodificado. A tentar conectar a {decodedIpPort.ip}:{decodedIpPort.port}...\n";

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(decodedIpPort.ip, decodedIpPort.port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
            {
                throw new TimeoutException("A tentativa de conexão excedeu o tempo limite.");
            }

            ClientLog += "Conexão bem-sucedida!\n";

            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            string message = UseCustomMessage && !string.IsNullOrWhiteSpace(CustomMessage)
                ? CustomMessage
                : $"Olá do cliente!";
            await writer.WriteAsync(message);
            ClientLog += $"Mensagem enviada: \"{message}\"\n";
        }
        catch (Exception ex)
        {
            ClientLog += $"Falha ao conectar: {ex.Message}\n";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool IsValidIpFormat(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length > 4) return false;
        
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (!byte.TryParse(part, out var num)) return false;
            if (num > 255) return false;
        }
        
        return true;
    }

    private IPAddress? GetLocalIpAddress()
    {
        try
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
        }
        catch { }
        return null;
    }

    private void LoadLocalIp()
    {
        var localIp = GetLocalIpAddress();
        if (localIp != null)
        {
            HostIp = localIp.ToString();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
