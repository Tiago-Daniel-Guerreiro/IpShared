namespace Invite_Generator;

using QRCoder;
using SIPSorcery.Net;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using ZXing;
using ZXing.Windows.Compatibility;

/// <summary>
/// Enumeração para especificar o formato de convite desejado.
/// </summary>
public enum InviteFormat
{
    Default, // 192.168.10:65535
    Base62, // qQ3GGXa8
    Base16, // 55F6E9B4C350
    Human, // voto-tira-tipo-sono-seco
    QrCodeBase64, // Imagem PNG do QR Code em Base64
    Unknown // Adicionado para casos em que o formato não pode ser determinado
}

/// <summary>
/// Gere e gere convites de conexão baseados no IP público do dispositivo.
/// </summary>
public class InviteGenerator
{
    // DTO interno para armazenar os resultados gerados.
    private class ConnectionInvite
    {
        public string DirectFormat { get; set; } = string.Empty;
        public string Base62Format { get; set; } = string.Empty;
        public string Base16Format { get; set; } = string.Empty;
        public string DicewareFormat { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;
    }

    private ushort _port;
    private IPAddress? _fixedIp;
    private ConnectionInvite? _invite;

    private const string StunServer = "stun.l.google.com:19302";
    private const string Base62Charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private InviteGenerator(ushort port) => _port = port;

    public static async Task<InviteGenerator> CreateAsync(ushort port)
    {
        var generator = new InviteGenerator(port);
        await generator.AtualizarIp();
        return generator;
    }

    /// <summary>
    /// NOVO: Cria uma instância de InviteGenerator com um IP fixo, sem usar STUN.
    /// Ideal para testes locais ou em redes controladas.
    /// </summary>
    /// <param name="fixedIp">O endereço IP a ser usado.</param>
    /// <param name="port">A porta a ser usada.</param>
    /// <returns>Uma instância de InviteGenerator pronta a usar.</returns>
    public static InviteGenerator CreateWithFixedIp(IPAddress fixedIp, ushort port)
    {
        var generator = new InviteGenerator(port)
        {
            _fixedIp = fixedIp
        };
        generator.AtualizarIpComIpFixo();
        return generator;
    }

    public async Task AtualizarIp(int port = -1)
    {
        if (port != -1) _port = (ushort)port;

        // Se um IP fixo foi definido, chama o método específico para ele.
        if (_fixedIp != null)
        {
            AtualizarIpComIpFixo();
            return;
        }

        IPAddress? publicIp = await GetPublicIpAsync();
        if (publicIp == null)
            throw new InvalidOperationException("Não foi possível obter o endereço IP público. Verifique a conexão ou firewall.");

        GerarConvitesInternos(publicIp);
    }

    /// <summary>
    /// NOVO: Define/atualiza os convites usando um IP fixo previamente definido.
    /// </summary>
    private void AtualizarIpComIpFixo()
    {
        if (_fixedIp == null)
            throw new InvalidOperationException("Nenhum IP fixo foi definido. Use CreateWithFixedIp ou defina o IP manualmente.");

        GerarConvitesInternos(_fixedIp);
    }

    /// <summary>
    /// Método centralizado para gerar os formatos de convite a partir de um IP.
    /// </summary>
    private void GerarConvitesInternos(IPAddress ip)
    {
        byte[] ipBytes = ip.GetAddressBytes();
        byte[] portBytes = BitConverter.GetBytes(_port);
        if (BitConverter.IsLittleEndian) Array.Reverse(portBytes);
        byte[] combinedBytes = ipBytes.Concat(portBytes).ToArray();

        _invite = new ConnectionInvite
        {
            DirectFormat = $"{ip}:{_port}",
            Base16Format = Convert.ToHexString(combinedBytes),
            Base62Format = ToBase62(combinedBytes),
            DicewareFormat = ToDiceware(Convert.ToHexString(combinedBytes)),
            QrCodeBase64 = GenerateQrCodeBase64($"{ip}:{_port}")
        };
    }

    public string ObterIp(InviteFormat format)
    {
        if (_invite == null) throw new InvalidOperationException("Os convites não foram gerados.");
        return format switch
        {
            InviteFormat.Default => _invite.DirectFormat,
            InviteFormat.Base62 => _invite.Base62Format,
            InviteFormat.Base16 => _invite.Base16Format,
            InviteFormat.Human => _invite.DicewareFormat,
            InviteFormat.QrCodeBase64 => _invite.QrCodeBase64,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }


    /// <summary>
    /// Tenta descodificar uma string de convite, detetando automaticamente o seu formato.
    /// </summary>
    /// <param name="inviteCode">A string de convite a ser descodificada.</param>
    /// <param name="decodedIpPort">O resultado da descodificação. Ficará vazio para formatos não reversíveis como 'Human'.</param>
    /// <returns>O InviteFormat detetado, ou InviteFormat.Unknown se a descodificação falhar.</returns>
    public static InviteFormat TryDecodeInvite(string inviteCode, out (IPAddress ip, ushort port) decodedIpPort)
    {
        inviteCode = inviteCode.Trim();
        decodedIpPort = default;

        //if(Is)
        // Filtro 1: Formato Human (Diceware). O mais distinto.
        // Ex: "flauta-escolha-fosco"
        var dicewareParts = inviteCode.Split('-');
        if (dicewareParts.Length == 5)
        {
            // O formato é identificado, mas não é reversível.
            // Apenas retornamos o tipo.
            return InviteFormat.Human;
        }

        // Filtro 2: Formato Default (IP:Porta). Usa Regex para precisão.
        // Ex: "85.246.233.180:60000"
        var defaultMatch = Regex.Match(inviteCode, @"^(\d{1,3}(\.\d{1,3}){3}):(\d{1,5})$");
        if (defaultMatch.Success && IPAddress.TryParse(defaultMatch.Groups[1].Value, out var ip) && ushort.TryParse(defaultMatch.Groups[3].Value, out var port))
        {
            decodedIpPort = (ip, port);
            return InviteFormat.Default;
        }

        // Filtro 3: Formato Base16 (Hexadecimal). Deve ter 12 caracteres hexadecimais.
        // Ex: "55F6E9B4C350"
        if (inviteCode.Length == 12 && Regex.IsMatch(inviteCode, @"^[0-9A-Fa-f]{12}$"))
        {
            try
            {
                decodedIpPort = DecodeBase16(inviteCode);
                return InviteFormat.Base16;
            }
            catch { /* Se falhar, não é este formato. */ }
        }

        // Filtro 4: Formato Base62. Apenas caracteres alfanuméricos.
        // Ex: "qQ3GGXa8"
        if (Regex.IsMatch(inviteCode, @"^[0-9a-zA-Z]+$"))
        {
            try
            {
                decodedIpPort = DecodeBase62(inviteCode);
                return InviteFormat.Base62;
            }
            catch { /* Se falhar, não é este formato. */ }
        }

        // Filtro 5: Formato QR Code (Base64). É uma string longa e com caracteres especiais.
        if (IsBase64String(inviteCode))
        {
            try
            {
                var qrContent = DecodeQrCodeFromBase64(inviteCode);
                // Chama-se a si mesmo para descodificar o conteúdo do QR Code.
                return TryDecodeInvite(qrContent, out decodedIpPort);
            }
            catch { /* Se a descodificação do QR falhar, não é este formato. */ }
        }

        // Se nenhum filtro funcionar, o formato é desconhecido.
        return InviteFormat.Unknown;
    }

    public static (IPAddress, ushort) DecodeBase16(string hex)
    {
        byte[] combinedBytes = Convert.FromHexString(hex);
        return BytesToIpPort(combinedBytes);
    }

    public static (IPAddress, ushort) DecodeBase62(string base62)
    {
        BigInteger number = 0;
        foreach (char c in base62)
        {
            number = number * Base62Charset.Length + Base62Charset.IndexOf(c);
        }
        byte[] combinedBytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (combinedBytes.Length < 6)
        {
            var paddedBytes = new byte[6];
            Buffer.BlockCopy(combinedBytes, 0, paddedBytes, 6 - combinedBytes.Length, combinedBytes.Length);
            combinedBytes = paddedBytes;
        }

        return BytesToIpPort(combinedBytes);
    }
    /// <summary>
    /// Descodifica uma string Base64 de um QR Code e retorna o seu conteúdo textual.
    /// Esta é a abordagem clássica, usando System.Drawing.Bitmap.
    /// </summary>
    /// <param name="base64QrCode">A string Base64 da imagem PNG do QR Code.</param>
    /// <returns>O conteúdo textual do QR Code.</returns>
    /// <summary>
    /// Descodifica uma string Base64 de um QR Code e retorna o seu conteúdo textual.
    /// Usa diretivas de compilação para ser multiplataforma.
    /// </summary>
    /// <param name="base64QrCode">A string Base64 da imagem PNG do QR Code.</param>
    /// <returns>O conteúdo textual do QR Code.</returns>
    public static string DecodeQrCodeFromBase64(string base64QrCode)
    {
        byte[] qrCodeBytes = Convert.FromBase64String(base64QrCode);

        // Declara a variável 'result' fora dos blocos para que seja acessível no final.
        Result? result;

#if ANDROID
        // --- Caminho para Android (e outras plataformas não-Windows) usando ImageSharp ---
        using (var memoryStream = new MemoryStream(qrCodeBytes))
        using (var image = Image.Load<L8>(memoryStream))
        {
            var reader = new BarcodeReader<L8>();
            result = reader.Decode(image);
        }
#else
        // --- Caminho para Windows usando System.Drawing.Bitmap ---
        using (var memoryStream = new MemoryStream(qrCodeBytes))
        using (var bitmap = new Bitmap(memoryStream))
        {
            // Este reader vem do pacote ZXing.Net.Bindings.Windows.Compatibility
            var reader = new BarcodeReader();
            result = reader.Decode(bitmap);
        }
#endif

        // A variável 'result' agora é acessível aqui, independentemente do caminho escolhido.
        if (result != null && !string.IsNullOrEmpty(result.Text))
        {
            return result.Text;
        }

        throw new InvalidDataException("Não foi possível extrair conteúdo do QR Code a partir da imagem.");
    }

    // --- MÉTODOS PRIVADOS AUXILIARES ---

    private static (IPAddress, ushort) BytesToIpPort(byte[] combinedBytes)
    {
        if (combinedBytes.Length != 6) throw new ArgumentException("Input data must be 6 bytes long.");

        var ipBytes = new byte[4];
        var portBytes = new byte[2];
        Buffer.BlockCopy(combinedBytes, 0, ipBytes, 0, 4);
        Buffer.BlockCopy(combinedBytes, 4, portBytes, 0, 2);

        if (BitConverter.IsLittleEndian) Array.Reverse(portBytes);

        return (new IPAddress(ipBytes), BitConverter.ToUInt16(portBytes, 0));
    }

    private async Task<IPAddress?> GetPublicIpAsync()
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

    private static string ToBase62(byte[] data)
    {
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

    private static string ToDiceware(string hexString)
    {
        var words = new string[3];
        for (int i = 0; i < 3; i++)
        {
            string hexPart = hexString.Substring(i * 4, 4);
            int value = int.Parse(hexPart, NumberStyles.HexNumber);
            //words[i] = DicewareWords[value % DicewareWords.Length];
        }
        return string.Join("-", words);
    }

    /// <summary>
    /// Codifica uma string hexadecimal de 12 caracteres para um formato de 6 palavras reversível.
    /// </summary>
    public static string ToReversibleDiceware6(string hexString)
    {
        if (hexString.Length != 12) throw new ArgumentException("Hex string must be 12 characters long.");

        var words = new string[6];
        for (int i = 0; i < 6; i++)
        {
            // Pega um bloco de 2 caracteres hex (um byte).
            string hexPart = hexString.Substring(i * 2, 2);
            // Converte o hex para um byte (0-255).
            byte index = byte.Parse(hexPart, NumberStyles.HexNumber);
            // Usa o byte como índice direto na lista de 256 palavras.
            //words[i] = ReversibleWordList256[index];
        }
        return string.Join("-", words);
    }

    /// <summary>
    /// Descodifica um convite de 6 palavras reversível de volta para a sua string hexadecimal.
    /// </summary>
    public static string DecodeReversibleDiceware6(string dicewareCode)
    {
        var parts = dicewareCode.Split('-');
        if (parts.Length != 6) throw new ArgumentException("Invalid 6-word reversible Diceware format.");

        var hexBuilder = new StringBuilder(12);
        for (int i = 0; i < 6; i++)
        {
            // Encontra o índice da palavra na lista.
            // int index = Array.IndexOf(ReversibleWordList256, parts[i]);
            // if (index == -1) throw new KeyNotFoundException($"A palavra '{parts[i]}' não foi encontrada na lista de palavras.");

            // Converte o índice (0-255) de volta para um hexadecimal de 2 caracteres (ex: 42 -> "2A").
            // hexBuilder.Append(index.ToString("X2"));
        }
        return hexBuilder.ToString();
    }


    private string GenerateQrCodeBase64(string content)
    {
        var qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeImageBytes = qrCode.GetGraphic(5);
        return Convert.ToBase64String(qrCodeImageBytes);
    }

    private static bool IsBase64String(string s)
    {
        s = s.Trim();
        return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }
}

