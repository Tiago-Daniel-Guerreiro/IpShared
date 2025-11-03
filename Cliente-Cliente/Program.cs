using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// IMPORTANTE: Certifique-se que está a usar a nova biblioteca refatorada!
using Invite_Generator.Refactored;

/// <summary>
/// Programa principal com um menu para usar as funcionalidades do InviteGenerator.
/// </summary>
public class Program
{
    // Ponto de entrada da aplicação
    public static async Task Main()
    {
        await ShowMainMenuAsync();
    }

    /// <summary>
    /// Exibe o menu principal e direciona o fluxo do programa.
    /// </summary>
    private static async Task ShowMainMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("--- Utilitário de Convites P2P ---");
            Console.WriteLine("Escolha uma opção:");
            Console.WriteLine("  [1] Criar convites a partir de um IP");
            Console.WriteLine("  [2] Obter IP a partir de um convite");
            Console.WriteLine("  [3] Modo Host/Cliente (Teste de Conexão)");
            Console.WriteLine("  [0] Sair");
            Console.Write("\nOpção: ");

            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await HandleGenerateInvitesAsync();
                    break;
                case "2":
                    HandleDecodeInvite();
                    break;
                case "3":
                    await ShowHostClientMenuAsync();
                    break;
                case "0":
                    return; // Sai do loop e encerra o programa
                default:
                    Console.WriteLine("Opção inválida. Pressione Enter para tentar novamente.");
                    Console.ReadLine();
                    break;
            }
        }
    }

    /// <summary>
    /// Opção 1: Gera e exibe convites.
    /// </summary>
    private static async Task HandleGenerateInvitesAsync()
    {
        Console.Clear();
        Console.WriteLine("--- Gerar Convites ---");

        Console.Write("Insira a porta (padrão: 60000): ");
        ushort port = ushort.TryParse(Console.ReadLine(), out var p) ? p : (ushort)60000;

        Console.Write("Usar IP público via STUN? (S/N, padrão: S): ");
        bool usePublicIp = !Console.ReadLine()?.Trim().Equals("N", StringComparison.OrdinalIgnoreCase) ?? true;

        try
        {
            Console.WriteLine("\nA gerar convites...");
            InviteGenerator generator;
            if (usePublicIp)
            {
                generator = await InviteGenerator.CreateAsync(port);
                Console.WriteLine("IP Público obtido com sucesso!");
            }
            else
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, port);
                Console.WriteLine("Usando IP de teste local (127.0.0.1).");
            }

            Console.WriteLine("\nConvites gerados:");
            Console.WriteLine($"  - Default:      {generator.ObterConvite(InviteFormat.Default)}");
            Console.WriteLine($"  - Base16 (Hex): {generator.ObterConvite(InviteFormat.Base16)}");
            Console.WriteLine($"  - Base62:       {generator.ObterConvite(InviteFormat.Base62)}");

            try
            {
                Console.WriteLine($"  - Humano:       {generator.ObterConvite(InviteFormat.Human)}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  - Humano:       ERRO - {ex.Message}");
                Console.ResetColor();
            }

            await OpenQrCodeFile(generator.ObterConvite(InviteFormat.QrCodeBase64));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nOcorreu um erro: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("\nPressione Enter para voltar ao menu principal.");
        Console.ReadLine();
    }

    /// <summary>
    /// Opção 2: Decodifica um convite inserido pelo utilizador.
    /// </summary>
    private static void HandleDecodeInvite()
    {
        Console.Clear();
        Console.WriteLine("--- Obter IP a partir de um Convite ---");
        Console.Write("Cole o código de convite (ou o conteúdo Base64 do QR Code) e pressione Enter: ");
        string? inviteCode = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            Console.WriteLine("Código inválido.");
        }
        else
        {
            var format = InviteGenerator.TryDecodeInvite(inviteCode, out var decodedIpPort);

            if (format == InviteFormat.Unknown)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Não foi possível decodificar o convite. Formato desconhecido ou inválido.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nConvite decodificado com sucesso!");
                Console.ResetColor();
                Console.WriteLine($"  - Formato detetado: {format}");
                Console.WriteLine($"  - Endereço IP:      {decodedIpPort.ip}");
                Console.WriteLine($"  - Porta:            {decodedIpPort.port}");
            }
        }

        Console.WriteLine("\nPressione Enter para voltar ao menu principal.");
        Console.ReadLine();
    }

    /// <summary>
    /// Opção 3: Exibe o submenu para teste de conexão Host/Cliente.
    /// </summary>
    private static async Task ShowHostClientMenuAsync()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("--- Modo Host/Cliente (Teste de Conexão) ---");
            Console.WriteLine("Escolha o modo de operação:");
            Console.WriteLine("  [1] Hospedar uma sessão (IP Público)");
            Console.WriteLine("  [2] Conectar a uma sessão");
            Console.WriteLine("  [3] Hospedar uma sessão de teste (IP Fixo: 127.0.0.1)");
            Console.WriteLine("  [0] Voltar ao menu principal");
            Console.Write("\nOpção: ");

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
                case "0":
                    return; // Volta ao menu principal
                default:
                    Console.WriteLine("Opção inválida.");
                    break;
            }
            Console.WriteLine("\nPressione Enter para continuar...");
            Console.ReadLine();
        }
    }

    #region Lógica Host/Cliente (Reutilizada do código original)

    /// <summary>
    /// Executa a aplicação no modo Host, gerando convites e aguardando conexões.
    /// </summary>
    private static async Task RunHostMode(bool useFixedIp)
    {
        const ushort port = 60000;

        try
        {
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

            Console.WriteLine("\n[Fase 2/3] Convites gerados. Partilhe um destes códigos com o outro utilizador:");
            Console.WriteLine($"  - Default: {generator.ObterConvite(InviteFormat.Default)}");
            Console.WriteLine($"  - Humano:  {generator.ObterConvite(InviteFormat.Human)}");
            await OpenQrCodeFile(generator.ObterConvite(InviteFormat.QrCodeBase64));

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
        Console.Write("Cole o código de convite e pressione Enter: ");
        string? inviteCode = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            Console.WriteLine("Código inválido.");
            return;
        }

        var format = InviteGenerator.TryDecodeInvite(inviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Não foi possível decodificar o código. Formato desconhecido ou inválido.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"Código decodificado como formato '{format}'. A tentar conectar a {decodedIpPort.ip}:{decodedIpPort.port}...");

        try
        {
            using var client = new TcpClient();
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
        catch (OperationCanceledException) { /* Ignora, é o encerramento normal */ }
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

    #endregion
}