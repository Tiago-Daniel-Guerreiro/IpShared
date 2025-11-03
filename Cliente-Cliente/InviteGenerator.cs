namespace Invite_Generator.Refactored;

using IpWordEncoder.Refactored;
using QRCoder;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZXing;

// ALTERAÇÃO: Diretivas de compilação ajustadas para serem mais genéricas.
#if WINDOWS
    // Usings específicos para Windows
    using System.Drawing;
    using ZXing.Windows.Compatibility;
#else
    // Usings multiplataforma (Linux, macOS, Android, etc.)
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
    using ZXing.ImageSharp;
#endif

/// <summary>
/// Enumeração para especificar o formato de convite desejado.
/// Combina os formatos de ambas as versões.
/// </summary>
public enum InviteFormat
{
    Default,      // 192.168.10.1:65535
    Base16,       // 55F6E9B4C350 (Hexadecimal)
    Base62,       // qQ3GGXa8
    Human,        // palavra-seis-palavra-cinco-palavra-quatro (Reversível)
    QrCodeBase64, // Imagem PNG do QR Code em Base64
    Unknown       // Formato não reconhecido
}

/// <summary>
/// Define o contrato para um conversor de convites.
/// Cada conversor é responsável por um formato específico.
/// </summary>
public interface IInviteConverter
{
    /// <summary>
    /// O formato que este conversor manipula.
    /// </summary>
    InviteFormat Format { get; }

    /// <summary>
    /// Verifica se uma string de convite corresponde ao formato deste conversor.
    /// </summary>
    /// <param name="inviteCode">O código do convite a ser verificado.</param>
    /// <returns>True se o formato corresponder, caso contrário, false.</returns>
    bool IsFormat(string inviteCode);

    /// <summary>
    /// Codifica um endereço IP e porta para o formato de string deste conversor.
    /// </summary>
    /// <param name="ip">O endereço IP.</param>
    /// <param name="port">A porta.</param>
    /// <returns>A string do convite codificada.</returns>
    string Encode(IPAddress ip, ushort port);

    /// <summary>
    /// Decodifica uma string de convite para um endereço IP e porta.
    /// </summary>
    /// <param name="inviteCode">A string do convite a ser decodificada.</param>
    /// <returns>Uma tupla contendo o IPAddress e a porta.</returns>
    (IPAddress ip, ushort port) Decode(string inviteCode);
}



/// <summary>
/// Conversor para o formato padrão "IP:Porta".
/// Ex: "127.0.0.1:50000"
/// </summary>
public class DefaultConverter : IInviteConverter
{
    private static readonly Regex FormatRegex = new(@"^(\d{1,3}(\.\d{1,3}){3}):(\d{1,5})$", RegexOptions.Compiled);
    public InviteFormat Format => InviteFormat.Default;

    public bool IsFormat(string inviteCode)
    {
        var match = FormatRegex.Match(inviteCode);
        return match.Success &&
               IPAddress.TryParse(match.Groups[1].Value, out _) &&
               ushort.TryParse(match.Groups[3].Value, out _);
    }

    public string Encode(IPAddress ip, ushort port) => $"{ip}:{port}";

    public (IPAddress ip, ushort port) Decode(string inviteCode)
    {
        var parts = inviteCode.Split(':');
        return (IPAddress.Parse(parts[0]), ushort.Parse(parts[1]));
    }
}

/// <summary>
/// Conversor para o formato Base16 (Hexadecimal).
/// Ex: "7F000001C350"
/// </summary>
public class Base16Converter : IInviteConverter
{
    public InviteFormat Format => InviteFormat.Base16;

    public bool IsFormat(string inviteCode) => inviteCode.Length == 12 && Regex.IsMatch(inviteCode, @"^[0-9A-Fa-f]{12}$");

    public string Encode(IPAddress ip, ushort port)
    {
        byte[] combinedBytes = InviteHelpers.IpPortToBytes(ip, port);
        return Convert.ToHexString(combinedBytes);
    }

    public (IPAddress ip, ushort port) Decode(string inviteCode)
    {
        byte[] combinedBytes = Convert.FromHexString(inviteCode);
        return InviteHelpers.BytesToIpPort(combinedBytes);
    }
}

/// <summary>
/// Conversor para o formato Base62.
/// Ex: "3j2dZpT8"
/// </summary>
public class Base62Converter : IInviteConverter
{
    private const string Base62Charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public InviteFormat Format => InviteFormat.Base62;

    public bool IsFormat(string inviteCode) => !string.IsNullOrEmpty(inviteCode) && inviteCode.All(c => Base62Charset.Contains(c));

    public string Encode(IPAddress ip, ushort port)
    {
        byte[] data = InviteHelpers.IpPortToBytes(ip, port);
        var number = new BigInteger(data, isUnsigned: true, isBigEndian: true);

        if (number == 0) return Base62Charset[0].ToString();

        var sb = new StringBuilder();
        while (number > 0)
        {
            number = BigInteger.DivRem(number, Base62Charset.Length, out var remainder);
            sb.Insert(0, Base62Charset[(int)remainder]);
        }
        return sb.ToString();
    }

    public (IPAddress ip, ushort port) Decode(string inviteCode)
    {
        BigInteger number = 0;
        foreach (char c in inviteCode)
        {
            number = number * Base62Charset.Length + Base62Charset.IndexOf(c);
        }
        byte[] combinedBytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);

        // Garante que o array de bytes tenha 6 bytes (padding à esquerda se necessário)
        if (combinedBytes.Length < 6)
        {
            var paddedBytes = new byte[6];
            Buffer.BlockCopy(combinedBytes, 0, paddedBytes, 6 - combinedBytes.Length, combinedBytes.Length);
            combinedBytes = paddedBytes;
        }

        return InviteHelpers.BytesToIpPort(combinedBytes);
    }
}

/// <summary>
/// Conversor para um formato "Humano" reversível usando 5 palavras, com e sem porta.
/// Ex: "palavra1-palavra2-palavra3-palavra4-palavra5"
/// </summary>
public class WordsConverter : IInviteConverter
{
    // Encoders da biblioteca, um para cada cenário (com e sem porta).
    private readonly WordEncoder _encoderWithPort;
    private readonly WordEncoder _encoderWithoutPort;

    /// <summary>
    /// Inicializa uma nova instância do WordsConverter.
    /// Este construtor carrega as listas de palavras de um diretório "Resources",
    /// configura e instancia os encoders necessários da biblioteca IpWordEncoder.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">Lançada se o diretório 'Resources' não for encontrado.</exception>
    /// <exception cref="InvalidOperationException">Lançada se nenhuma lista de palavras válida for carregada.</exception>
    public WordsConverter()
    {
        // 1. Configurar o caminho para as listas de palavras.
        // A biblioteca espera um caminho; aqui assumimos um diretório "Resources" relativo à execução.
        string resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        if (!Directory.Exists(resourcePath))
        {
            // Se o diretório não existir, lança uma exceção clara.
            // Para usar esta classe, é preciso garantir que o diretório "Resources" com os ficheiros "Words_X.txt" exista.
            throw new DirectoryNotFoundException($"O diretório de recursos não foi encontrado no caminho esperado: '{resourcePath}'.");
        }

        // 2. Instanciar a configuração e carregar as palavras usando o loader da biblioteca.
        var encoderConfig = new WordEncoderConfig();
        List<string[]> wordLists = WordListLoader.LoadFromDirectory(resourcePath, encoderConfig.DictionaryWordCount);

        if (wordLists.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma lista de palavras válida foi carregada. Verifique o conteúdo do diretório 'Resources'.");
        }

        // 3. Criar os mapas de palavras (word -> index) conforme a lógica da biblioteca.
        ReadOnlyCollection<Dictionary<string, int>> wordMaps = wordLists.Select(list =>
            list.Select((word, index) => new { word, index })
                .ToDictionary(item => item.word, item => item.index, StringComparer.InvariantCultureIgnoreCase)
        ).ToList().AsReadOnly();

        // 4. Instanciar as duas estratégias de codificação da biblioteca.
        var ipPortStrategy = new IpAndPortEncodingStrategy(encoderConfig, wordLists, wordMaps);
        var ipOnlyStrategy = new IpOnlyEncodingStrategy(wordLists, wordMaps);

        // 5. Guardar uma instância do WordEncoder para cada estratégia.
        _encoderWithPort = new WordEncoder(ipPortStrategy);
        _encoderWithoutPort = new WordEncoder(ipOnlyStrategy);
    }

    public InviteFormat Format => InviteFormat.Human;

    /// <summary>
    /// Verifica se a string de convite tem o formato básico esperado (5 palavras separadas por hífen).
    /// Esta é uma verificação rápida para evitar exceções desnecessárias no método Decode.
    /// </summary>
    public bool IsFormat(string inviteCode)
    {
        if (string.IsNullOrWhiteSpace(inviteCode))
            return false;

        string[] parts = inviteCode.Split('-');
        // A biblioteca usa 5 palavras para ambos os modos.
        return parts.Length == 5;
    }

    /// <summary>
    /// Codifica um endereço IP e uma porta numa string de 5 palavras.
    /// Utiliza o encoder "com porta" da biblioteca.
    /// </summary>
    /// <param name="ip">O endereço IP a codificar.</param>
    /// <param name="port">A porta a codificar.</param>
    /// <returns>A string codificada.</returns>
    public string Encode(IPAddress ip, ushort port)
    {
        // Chama diretamente o método Encode da instância do WordEncoder configurada com a estratégia de IP e Porta.
        // Usa o listId = 0 por defeito, pois a interface não especifica qual usar.
        return _encoderWithPort.Encode(ip, port, listId: 0);
    }

    // NOTA: Poderia adicionar aqui um overload para o caso "sem porta" se a interface permitisse.
    // public string Encode(IPAddress ip)
    // {
    //     return _encoderWithoutPort.Encode(ip, listId: 0);
    // }

    /// <summary>
    /// Descodifica uma string de 5 palavras para um endereço IP e uma porta.
    /// Utiliza o decoder "com porta" da biblioteca.
    /// </summary>
    /// <param name="inviteCode">A string de convite a descodificar.</param>
    /// <returns>Um tuplo contendo o IP e a porta descodificados.</returns>
    /// <exception cref="FormatException">Lançada se o código não estiver num formato válido ou não puder ser descodificado.</exception>
    public (IPAddress ip, ushort port) Decode(string inviteCode)
    {
        if (!IsFormat(inviteCode))
            throw new System.FormatException($"O código de convite '{inviteCode}' não está no formato de 5 palavras válido.");

        try
        {
            // Chama diretamente o método Decode do WordEncoder.
            var (decodedIp, decodedPort, _) = _encoderWithPort.Decode(inviteCode);

            // A estratégia com porta deve sempre retornar uma porta. Se não o fizer, é um estado inesperado.
            if (!decodedPort.HasValue)
            {
                throw new InvalidOperationException("A descodificação resultou num valor de porta nulo, o que é inesperado para este formato.");
            }

            return (decodedIp, decodedPort.Value);
        }
        // A biblioteca pode lançar KeyNotFoundException ou outras se as palavras não existirem.
        // Capturamos e encapsulamos numa FormatException para o consumidor desta classe.
        catch (Exception ex) when (ex is KeyNotFoundException || ex is ArgumentException)
        {
            throw new System.FormatException($"Falha ao descodificar o código de convite '{inviteCode}'. Detalhes: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Gere e descodifica convites de conexão baseados no IP e porta,
/// utilizando uma arquitetura modular de conversores.
/// </summary>
public class InviteGenerator
{
    private readonly IPAddress _ip;
    private readonly ushort _port;
    private readonly Dictionary<InviteFormat, IInviteConverter> _converters;
    private static readonly List<IInviteConverter> AllConverters;

    private const string StunServer = "stun.l.google.com:19302";
    
    // Inicializador estático para popular a lista de conversores para o método estático de decodificação.
    static InviteGenerator()
    {
        AllConverters = new List<IInviteConverter>
        {
            new DefaultConverter(),
            new WordsConverter(), // Prioridade alta por ser distintivo (contém '-')
            new Base16Converter(),
            new Base62Converter()
            // Adicione novos conversores aqui. A ordem importa para a deteção.
        };
    }

    private InviteGenerator(IPAddress ip, ushort port)
    {
        _ip = ip;
        _port = port;
        // Cria um dicionário para acesso rápido pelo enum
        _converters = AllConverters.ToDictionary(c => c.Format, c => c);
    }

    /// <summary>
    /// Cria uma instância do gerador, descobrindo o IP público via STUN.
    /// </summary>
    /// <param name="port">A porta a ser usada no convite.</param>
    /// <returns>Uma instância de InviteGenerator.</returns>
    /// <exception cref="InvalidOperationException">Se não for possível obter o IP público.</exception>
    public static async Task<InviteGenerator> CreateAsync(ushort port)
    {
        IPAddress? publicIp = await GetPublicIpAsync();
        if (publicIp == null)
        {
            throw new InvalidOperationException("Não foi possível obter o endereço IP público. Verifique a conexão com a internet ou as configurações de firewall.");
        }
        return new InviteGenerator(publicIp, port);
    }

    /// <summary>
    /// Cria uma instância do gerador com um IP fixo, ideal para testes locais ou redes controladas.
    /// </summary>
    /// <param name="fixedIp">O endereço IP a ser usado.</param>
    /// <param name="port">A porta a ser usada.</param>
    /// <returns>Uma instância de InviteGenerator.</returns>
    public static InviteGenerator CreateWithFixedIp(IPAddress fixedIp, ushort port)
    {
        return new InviteGenerator(fixedIp, port);
    }

    /// <summary>
    /// Obtém o convite no formato especificado.
    /// </summary>
    /// <param name="format">O formato desejado para o convite.</param>
    /// <returns>A string do convite no formato solicitado.</returns>
    public string ObterConvite(InviteFormat format)
    {
        if (format == InviteFormat.QrCodeBase64)
        {
            // O QR Code sempre codifica o formato mais universal e legível (Default).
            string contentToEncode = ObterConvite(InviteFormat.Default);
            return GenerateQrCodeBase64(contentToEncode);
        }

        if (_converters.TryGetValue(format, out var converter))
        {
            return converter.Encode(_ip, _port);
        }

        throw new ArgumentOutOfRangeException(nameof(format), "Formato de convite não suportado.");
    }

    /// <summary>
    /// Tenta decodificar uma string de convite, detetando automaticamente o seu formato.
    /// </summary>
    /// <param name="inviteCode">A string de convite a ser decodificada.</param>
    /// <param name="decodedIpPort">O resultado da decodificação (IP e porta).</param>
    /// <returns>O InviteFormat detetado, ou InviteFormat.Unknown se a decodificação falhar.</returns>
    public static InviteFormat TryDecodeInvite(string inviteCode, out (IPAddress ip, ushort port) decodedIpPort)
    {
        inviteCode = inviteCode.Trim();
        decodedIpPort = default;

        // 1. Caso especial: QR Code (Base64)
        if (InviteHelpers.IsBase64String(inviteCode))
        {
            try
            {
                var qrContent = DecodeQrCodeFromBase64(inviteCode);
                // Chama-se recursivamente para decodificar o conteúdo do QR Code.
                // O formato retornado será o do conteúdo, não QrCodeBase64.
                return TryDecodeInvite(qrContent, out decodedIpPort);
            }
            catch { /* Ignora falha e tenta outros formatos */ }
        }

        // 2. Itera sobre os conversores registados
        // A ordem na lista 'AllConverters' é importante para evitar falsos positivos (ex: Base62 vs Base16)
        foreach (var converter in AllConverters)
        {
            if (converter.IsFormat(inviteCode))
            {
                try
                {
                    decodedIpPort = converter.Decode(inviteCode);
                    return converter.Format; // Sucesso!
                }
                catch
                {
                    // A deteção foi positiva, mas a decodificação falhou (código inválido).
                    // Continua para o próximo conversor, pode ser um caso de sobreposição de regras.
                }
            }
        }
        
        return InviteFormat.Unknown;
    }

    // --- MÉTODOS PRIVADOS E AUXILIARES ---

    private static async Task<IPAddress?> GetPublicIpAsync()
    {
        var pc = new RTCPeerConnection(new RTCConfiguration { iceServers = new List<RTCIceServer> { new RTCIceServer { urls = StunServer } } });
        var tcs = new TaskCompletionSource<IPAddress?>();

        pc.onicecandidate += (candidate) =>
        {
            if (candidate?.type == RTCIceCandidateType.srflx && IPAddress.TryParse(candidate.address, out var publicIp))
            {
                tcs.TrySetResult(publicIp);
            }
        };

        pc.onicegatheringstatechange += (state) =>
        {
            if (state == RTCIceGatheringState.complete) tcs.TrySetResult(null);
        };

        await pc.createDataChannel("dummy");
        var offer = pc.createOffer();
        await pc.setLocalDescription(offer);

        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (pc.connectionState != RTCPeerConnectionState.closed) pc.close();

        return completedTask == tcs.Task ? await tcs.Task : null;
    }

    private string GenerateQrCodeBase64(string content)
    {
        var qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImageBytes = qrCode.GetGraphic(5);
        return Convert.ToBase64String(qrCodeImageBytes);
    }

    /// <summary>
    /// Descodifica uma string Base64 de um QR Code e retorna o seu conteúdo textual.
    /// Usa diretivas de compilação para ser multiplataforma.
    /// </summary>
    public static string DecodeQrCodeFromBase64(string base64QrCode)
    {
        byte[] qrCodeBytes = Convert.FromBase64String(base64QrCode);
        Result? result;

    #if WINDOWS
        // --- Caminho para Windows usando System.Drawing.Bitmap ---
        using var memoryStream = new MemoryStream(qrCodeBytes);
        using var bitmap = new System.Drawing.Bitmap(memoryStream);
        
        // ALTERAÇÃO: Usar o nome completo da classe para evitar ambiguidade e a dependência do PresentationCore.
        var reader = new ZXing.Windows.Compatibility.BarcodeReader();
        
        result = reader.Decode(bitmap);
    #else
        // --- Caminho para Linux, macOS, Android, etc. usando ImageSharp ---
        using var memoryStream = new MemoryStream(qrCodeBytes);
        using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.L8>(memoryStream);

        // ALTERAÇÃO: Usar o nome completo da classe para resolver a ambiguidade.
        var reader = new ZXing.ImageSharp.BarcodeReader<SixLabors.ImageSharp.PixelFormats.L8>();
        
        result = reader.Decode(image);
    #endif

        if (result != null && !string.IsNullOrEmpty(result.Text))
        {
            return result.Text;
        }

        throw new InvalidDataException("Não foi possível extrair conteúdo do QR Code a partir da imagem.");
    }
}

/// <summary>
/// Classe auxiliar com métodos estáticos partilhados pelos conversores.
/// </summary>
internal static class InviteHelpers
{
    /// <summary>
    /// Converte um IP e porta para um array de 6 bytes.
    /// </summary>
    public static byte[] IpPortToBytes(IPAddress ip, ushort port)
    {
        byte[] ipBytes = ip.GetAddressBytes();
        byte[] portBytes = BitConverter.GetBytes(port);
        if (BitConverter.IsLittleEndian) Array.Reverse(portBytes);
        return ipBytes.Concat(portBytes).ToArray();
    }

    /// <summary>
    /// Converte um array de 6 bytes para um IP e porta.
    /// </summary>
    public static (IPAddress, ushort) BytesToIpPort(byte[] combinedBytes)
    {
        if (combinedBytes.Length != 6) throw new ArgumentException("Os dados de entrada devem ter 6 bytes.", nameof(combinedBytes));

        var ipBytes = new byte[4];
        var portBytes = new byte[2];
        Buffer.BlockCopy(combinedBytes, 0, ipBytes, 0, 4);
        Buffer.BlockCopy(combinedBytes, 4, portBytes, 0, 2);

        if (BitConverter.IsLittleEndian) Array.Reverse(portBytes);

        return (new IPAddress(ipBytes), BitConverter.ToUInt16(portBytes, 0));
    }
    
    /// <summary>
    /// Verifica se uma string parece ser uma string Base64 válida.
    /// </summary>
    public static bool IsBase64String(string s)
    {
        s = s.Trim();
        return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }
}
