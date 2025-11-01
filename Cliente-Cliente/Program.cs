using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Ip_Word_Encoder_V3;
using Invite_Generator;

/// <summary>
/// Programa principal para hospedar uma sessão ou conectar-se a uma, usando o InviteGenerator.
/// </summary>
public class Program
{
    public static async Task Main()
    {
        Program_V3.Main();
        Console.WriteLine("Pressione Enter para iniciar o utilitário de conexão P2P...");
        Console.ReadLine();
        Console.WriteLine("--- Utilitário de Conexão P2P ---");
        Console.WriteLine("Escolha o modo de operação:");
        Console.WriteLine("  [1] Hospedar uma sessão (Gerar convites)");
        Console.WriteLine("  [2] Conectar a uma sessão (Usar um código de convite)");
        Console.WriteLine("  [3] Hospedar uma sessão de teste local (IP Fixo: 127.0.0.1)");
        Console.Write("Opção: ");

        string? choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await RunHostMode(useFixedIp: false);
                break;
            case "2":
                await RunClientMode();
                break;
            case "3":
                await RunHostMode(useFixedIp: true);
                break;
            default:
                Console.WriteLine("Opção inválida.");
                break;
        }
    }

    /// <summary>
    /// Executa a aplicação no modo Host, gerando convites e aguardando conexões.
    /// </summary>
    private static async Task RunHostMode(bool useFixedIp)
    {
        const ushort port = 6000;

        try
        {
            // --- Fase 1: Inicializar o generator ---
            Console.WriteLine("\n[Fase 1/3] A gerar convites...");
            InviteGenerator generator;
            if (useFixedIp)
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, port);
                Console.WriteLine("Modo de teste local: Usando IP fixo 127.0.0.1.");
            }
            else
            {
                generator = await InviteGenerator.CreateAsync(port);
            }

            // --- Fase 2: Listar os resultados ---
            Console.WriteLine("\n[Fase 2/3] Convites gerados. Partilhe um destes códigos com o outro utilizador:");
            Console.WriteLine($"  - Default:      {generator.ObterIp(InviteFormat.Default)}");
            Console.WriteLine($"  - Base16 (Hex): {generator.ObterIp(InviteFormat.Base16)}");
            Console.WriteLine($"  - Base62:       {generator.ObterIp(InviteFormat.Base62)}");
            Console.WriteLine($"  - Humano:       {generator.ObterIp(InviteFormat.Human)}");
            await OpenQrCodeFile(generator.ObterIp(InviteFormat.QrCodeBase64));

            // --- Fase 3: Iniciar o servidor e aguardar conexões ---
            Console.WriteLine($"\n[Fase 3/3] Servidor de escuta ativo em '0.0.0.0:{port}'.");
            Console.WriteLine("A aguardar conexões... Pressione CTRL + C para encerrar.");

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            await StartTcpListener(port, cts.Token);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nOcorreu um erro crítico no modo Host: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            Console.WriteLine("\nModo Host encerrado.");
        }
    }

    /// <summary>
    /// Executa a aplicação no modo Cliente, pedindo um código para se conectar.
    /// </summary>
    private static async Task RunClientMode()
    {
        Console.WriteLine("\n--- Modo Cliente ---");
        Console.Write("Cole o código de convite (ou o conteúdo Base64 do QR Code) e pressione Enter: ");
        string? inviteCode = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            Console.WriteLine("Código inválido.");
            return;
        }

        // Tenta descodificar o código inserido
        var format = InviteGenerator.TryDecodeInvite(inviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Não foi possível descodificar o código de convite. Formato desconhecido ou inválido.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Código descodificado como formato '{format}'. A tentar conectar a {decodedIpPort.ip}:{decodedIpPort.port}...");

        try
        {
            using var client = new TcpClient();
            // Adiciona um timeout para a conexão
            var connectTask = client.ConnectAsync(decodedIpPort.ip, decodedIpPort.port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
            {
                throw new TimeoutException("A tentativa de conexão excedeu o tempo limite.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Conexão bem-sucedida!");
            Console.ResetColor();

            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            string message = $"Olá do cliente! O código '{format}' funcionou.";
            await writer.WriteAsync(message);
            Console.WriteLine($"Mensagem enviada: \"{message}\"");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Falha ao conectar: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Inicia um servidor TCP que escuta por conexões e imprime as mensagens recebidas.
    /// </summary>
    private static async Task StartTcpListener(ushort port, CancellationToken token)
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
                        string message = await streamReader.ReadToEndAsync();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n[SERVIDOR] Requisição recebida de {client.Client.RemoteEndPoint}: \"{message.Trim()}\"");
                        Console.ResetColor();
                        Console.Write("A aguardar conexões... Pressione CTRL + C para encerrar.\n");
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* Terminação limpa e esperada */ }
        catch (Exception ex) { Console.WriteLine($"Erro no listener: {ex.Message}"); }
        finally { listener.Stop(); }
    }

    /// <summary>
    /// Salva a imagem do QR Code num ficheiro temporário e abre-o.
    /// </summary>
    private static async Task OpenQrCodeFile(string base64)
    {
        try
        {
            string filePath = Path.Combine(Path.GetTempPath(), $"invite_{Guid.NewGuid()}.png");
            byte[] imageBytes = Convert.FromBase64String(base64);
            await File.WriteAllBytesAsync(filePath, imageBytes);
            new Process { StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true } }.Start();
            Console.WriteLine("  - QR Code: Imagem aberta no seu visualizador padrão.");
        }
        catch (Exception)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  - Aviso: Não foi possível abrir automaticamente a imagem do QR Code.");
            Console.ResetColor();
        }
    }
}