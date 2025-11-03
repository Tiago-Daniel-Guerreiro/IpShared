using Avalonia.Threading;
using Invite_Generator.Refactored;
using ReactiveUI;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IpShared.ViewModels;

public class HostClientViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cts;

    // Propriedades do Host
    private bool _useFixedIp = false;
    public bool UseFixedIp { get => _useFixedIp; set => this.RaiseAndSetIfChanged(ref _useFixedIp, value); }

    private bool _isHostRunning = false;
    public bool IsHostRunning { get => _isHostRunning; set => this.RaiseAndSetIfChanged(ref _isHostRunning, value); }

    private string? _hostInviteCode;
    public string? HostInviteCode { get => _hostInviteCode; set => this.RaiseAndSetIfChanged(ref _hostInviteCode, value); }

    private string _hostLog = "O servidor está parado.";
    public string HostLog { get => _hostLog; set => this.RaiseAndSetIfChanged(ref _hostLog, value); }
    
    // Propriedades do Cliente
    private string? _clientInviteCode;
    public string? ClientInviteCode { get => _clientInviteCode; set => this.RaiseAndSetIfChanged(ref _clientInviteCode, value); }

    private string _clientLog = "Pronto para conectar.";
    public string ClientLog { get => _clientLog; set => this.RaiseAndSetIfChanged(ref _clientLog, value); }

    private bool _isConnecting = false;
    public bool IsConnecting { get => _isConnecting; set => this.RaiseAndSetIfChanged(ref _isConnecting, value); }

    public async Task StartHostAsync()
    {
        if (IsHostRunning) return;
        
        IsHostRunning = true;
        _cts = new CancellationTokenSource();
        const ushort port = 60000;
        HostLog = "[Fase 1/3] A gerar convites...\n";

        try
        {
            InviteGenerator generator;
            if (UseFixedIp)
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, port);
                HostLog += "Modo de teste local: Usando IP fixo 127.0.0.1.\n";
            }
            else
            {
                generator = await InviteGenerator.CreateAsync(port);
            }
            
            HostInviteCode = $"Humano: {generator.ObterConvite(InviteFormat.Human)}\n" +
                             $"Default: {generator.ObterConvite(InviteFormat.Default)}";
            
            HostLog += "[Fase 2/3] Convites gerados. Partilhe um código com o cliente.\n";
            HostLog += $"[Fase 3/3] Servidor ativo em 0.0.0.0:{port}. A aguardar conexões...\n";
            
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
                            HostLog += $"[SERVIDOR] Requisição de {client.Client.RemoteEndPoint}: \"{message.Trim()}\"\n";
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
            string message = $"Olá do cliente! O código '{format}' funcionou.";
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

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
