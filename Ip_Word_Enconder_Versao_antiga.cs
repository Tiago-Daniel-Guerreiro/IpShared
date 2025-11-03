namespace Ip_Word_Encoder_V3;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

// V3 = Funcional, com maiusculas corretamente aplicadas, e modular
// V2 = Funcional mas maiusculas vieram com base nos 3 ultimos bits ao invés dos 3 bits maiores, e em 1 unica class
// V1 = Esboço inicial com vários erros de lógica, nem funcionava
public static class SimpleLogger
{
    private static readonly List<string> _buffer = new List<string>(1024);

    private static readonly string _logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "log.txt"
    );

    private const int BufferCapacity = 1024;

    public static void Add(string logEntry)
    {
        _buffer.Add(logEntry);

        if (_buffer.Count >= BufferCapacity)
        {

            bool writtenSuccessfully = false;
            while (!writtenSuccessfully)
            {
                try
                {
                    File.AppendAllLines(_logFilePath, _buffer);

                    writtenSuccessfully = true;
                }
                catch (IOException)
                {
                    Console.WriteLine($"Aviso: O ficheiro '{_logFilePath}' está em uso.");
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERRO INESPERADO] Falha ao escrever no ficheiro de log: {ex.Message}");
                    break;
                }
            }

            if (writtenSuccessfully)
                _buffer.Clear();
        }
    }
}

public static class Program_V3
{
    private static WordEncoder? _defaultEncoder;
    private static readonly Random _randomGenerator = new();

    public static void Main_V3()
    {
        var defaultConfig = new WordEncoderConfig();

        string resourcePath = "C:\\Users\\Tiago G\\source\\repos\\Cliente-Cliente\\Cliente-Cliente\\Resources";
        List<string[]> wordLists = WordListLoader.LoadFromDirectory(resourcePath, defaultConfig.ListWordCount);

        if (wordLists.Count == 0)
        {
            Console.WriteLine("ERRO: Nenhuma lista de palavras válida foi encontrado. O programa não pode continuar.");
            return;
        }

        _defaultEncoder = new WordEncoder(defaultConfig, wordLists);

        // Faz 1000 +2 testes aleatórios
        int testes = 1000;
        int erros = 0;

        SimpleLogger.Add("A iniciar testes...");
        SimpleLogger.Add("Valor mais baixo: ");

        if (Teste("0.0.0.0", 0))
            erros++;

        Console.Write("Valor mais alto: ");
        if (Teste("255.255.255.255", ushort.MaxValue, 15))
            erros++;

        Random random = new();
        for (int i = 0; i < testes; i++)
        {
            if (Teste(IpAleatorio(), PortaAleatoria(), IdAleatorio()))
                erros++;
        }

        // Para testar intervalos de ips/portas
        /*
            for(int indice_List=0; indice_List < 16; indice_List++)
            {
                for (ushort port = 0; port < ushort.MaxValue; port++)
                {
                    for (int b1 = 0; b1 < 256; b1++)
                    {
                        for (int b2 = 0; b2 < 256; b2++)
                        {
                            for (int b3 = 0; b3 < 256; b3++)
                            {
                                for (int b4 = 0; b4 < 256; b4++)
                                {
                                    if (Teste($"{b1}.{b2}.{b3}.{b4}", port, indice_List))
                                        erros++;
                                }
                            }
                        }
                    }
                }
            }
        */

        SimpleLogger.Add($"\nForam feitos {testes + 2} testes, e foram encontrados {erros} erros.");

        if (erros == 0)
            SimpleLogger.Add("Todos os testes passaram com sucesso!");

        SimpleLogger.Add("\nExemplo de Uso (Verificação humana, ao invés de só automática)");
        var ipExemplo = IpAleatorio();
        var portaExemplo = PortaAleatoria();
        int idExemplo = 0;

        string encoded = _defaultEncoder.Encode(IPAddress.Parse(ipExemplo), portaExemplo, idExemplo);
        SimpleLogger.Add($"\t{ipExemplo}:{portaExemplo} (ID: {idExemplo}) = {encoded}");

        var (decodedIp, decodedPort, decodedId) = _defaultEncoder.Decode(encoded);
        SimpleLogger.Add($"\t{encoded} = {decodedIp}:{decodedPort} (ID: {decodedId})");
    }

    public static bool Teste(string ipStr, ushort port, int listId = 0)
    {
        if (_defaultEncoder == null)
        {
            Console.WriteLine("ERRO: O codificador não foi inicializado.");
            return true;
        }

        var ip = IPAddress.Parse(ipStr);

        try
        {
            string encoded = _defaultEncoder.Encode(ip, port, listId);
            var (decodedIp, decodedPort, decodedId) = _defaultEncoder.Decode(encoded);

            if (decodedIp.Equals(ip) && decodedPort == port && decodedId == listId)
            {
                SimpleLogger.Add($"\t{encoded} = {decodedIp}:{decodedPort} (ID: {decodedId})");
                return false;
            }

            Console.WriteLine($"-!- FALHA: {ipStr}:{port} | Descodificado para: {decodedIp}:{decodedPort}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"-!- ERRO no teste com {ipStr}:{port}: {ex.Message}");
            return true;
        }
    }

    public static string IpAleatorio()
    {
        return $"{_randomGenerator.Next(0, 256)}.{_randomGenerator.Next(0, 256)}.{_randomGenerator.Next(0, 256)}.{_randomGenerator.Next(0, 256)}";
    }
    public static ushort PortaAleatoria()
    {
        return (ushort)_randomGenerator.Next(0, ushort.MaxValue + 1);
    }
    public static int IdAleatorio()
    {
        return _randomGenerator.Next(0, 15);
    }
}

public class WordEncoder
{
    private readonly WordEncoderConfig _config;
    private readonly List<string[]> _wordLists;

    public WordEncoder(WordEncoderConfig config, List<string[]> wordLists)
    {
        _config = config;
        _wordLists = wordLists;
        if (_wordLists.Count == 0)
            throw new ArgumentException("A lista de palavras não pode estar vazia.", nameof(wordLists));
    }

    public string Encode(IPAddress ip, ushort port, int listId = 0)
    {
        if (listId < 0 || listId >= _wordLists.Count)
            throw new ArgumentOutOfRangeException(nameof(listId), $"ID do dicionário é inválido. ({listId})");

        var ipBits = new BitArray(ip.GetAddressBytes());
        var portBits = new BitArray(BitConverter.GetBytes(port));

        // Separa a porta em 2 partes, nos 3 bits que ficam nas "maiusculas" e os 13 bits que ficam junto aos do ip

        // O BitArray guarda os bits mais significativos no final
        var portMetadataBits = new BitArray(_config.PortMetadataBitCount);
        var portDataBits = new BitArray(_config.PortDataBitCount);

        for (int i = 0; i < _config.PortDataBitCount; i++)
            portDataBits[i] = portBits[i]; // Os primeiros 13 bits (0-12) são dados

        for (int i = 0; i < _config.PortMetadataBitCount; i++)
            portMetadataBits[i] = portBits[_config.PortDataBitCount + i]; // Os últimos 3 bits (13, 14, 15) são metadados

        // Junta os dados principais, ip + porta
        var mainDataBits = BitArrayUtils.Concatenate(ipBits, portDataBits); // 32 + 13 = 45 bits

        // Inverte a fita para ter a ordem de mais "importante" no inicio
        var logicalBits = BitArrayUtils.Reverse(mainDataBits);

        // Divide em blocos, e cria as palavras
        List<BitArray> chunks = BitArrayUtils.Split(logicalBits, _config.ChunkBitCount, out _);
        List<int> indices = BitArrayUtils.ToInts(chunks);

        string[] wordList = _wordLists[listId];
        List<string> outputWords = new(indices.Count);

        foreach (int indice in indices)
            outputWords[indice] = wordList[indice];

        // Aplica os "metadados" do id e do "resto" dos bits da porta
        BitArray idBits = new([listId]) { Length = _config.IdBitCount };
        ApplyMetadata(outputWords, idBits, portMetadataBits);

        return string.Join("-", outputWords);
    }

    public (IPAddress ip, ushort port, int id) Decode(string encoded)
    {
        string[] encodedWords = encoded.Split('-');

        // Pega os "metadados" que estão nas maiusculas
        (BitArray idBits, BitArray portMetadataBits) = ExtractMetadata(encodedWords);
        int listId = BitArrayUtils.ToInts([idBits])[0];

        if (listId < 0 || listId >= _wordLists.Count)
            throw new KeyNotFoundException($"A lista de palavras {listId} não foi encontrado.");

        // Reconstroi os 45 bits da mensagem

        string[] wordList = _wordLists[listId];
        List<BitArray> bitChunks = new(encodedWords.Length - 1);

        for (int indice_word = 0; indice_word < encoded.Length; indice_word++)
        {
            int index = Array.IndexOf(wordList, encodedWords[indice_word].ToLowerInvariant());

            if (index == -1)
                throw new KeyNotFoundException($"Palavra '{encodedWords[indice_word]}' não encontrada.");

            bitChunks[indice_word] = new BitArray([index]) { Length = _config.ChunkBitCount };
        }

        BitArray? logicalBits = BitArrayUtils.Concatenate(bitChunks.ToArray());
        BitArray? mainDataBits = BitArrayUtils.Reverse(logicalBits); // Volta para a ordem do BitArray, maior no fim

        // Separa os dados do ip (45 bits) e os dados da porta (13 bits)
        BitArray? ipBits = new(_config.IpBitCount);
        BitArray? portDataBits = new(_config.PortDataBitCount);

        for (int i = 0; i < _config.IpBitCount; i++)
            ipBits[i] = mainDataBits[i];

        for (int i = 0; i < _config.PortDataBitCount; i++)
            portDataBits[i] = mainDataBits[_config.IpBitCount + i];

        // Junta os dados da porta com os "metadados"
        BitArray? portBits = BitArrayUtils.Concatenate(portDataBits, portMetadataBits);

        // Converte os bits de volta para o tipo correto
        byte[] ipBytes = new byte[_config.IpByteCount];
        ipBits.CopyTo(ipBytes, 0);

        byte[] portBytes = new byte[_config.PortByteCount];
        portBits.CopyTo(portBytes, 0);

        return (new IPAddress(ipBytes), BitConverter.ToUInt16(portBytes, 0), listId);
    }
    private void ApplyMetadata(List<string> words, BitArray idBits, BitArray portMetadataBits)
    {
        for (int i = 0; i < idBits.Length && i < words.Count; i++)
        {
            if (idBits[i])
                words[i] = Capitalize(words[i], 0);
        }
        /*
            Pode ter 16 ids diferentes, 0-15.
            Aproveita que o BitArray guarda na ordem "contrária" para aplicar os menores valores de id ás palavras iniciais ao invés das finais
                0000 - a b c d - 0
                0001 - A b c d - 1
                0010 - a B c d - 2
                0011 - A B c d - 3
                0100 - a b C d - 4
                0101 - A b C d - 5
                0110 - a B C d - 6
                0111 - A B C d - 7
                1000 - a b c D - 8
                1001 - A b c D - 9
                1010 - a B c D - 10
                1011 - A B c D - 11
                1100 - a b C D - 12
                1101 - A b C D - 13
                1110 - a B C D - 14
                1111 - A B C D - 15
        */

        // Porta
        if (portMetadataBits.Length > 0 && words.Count != 0)
        {
            string lastWord = words.Last();

            if (portMetadataBits.Length > 0 && lastWord.Length > 0 && _config.IdBitCount < words.Count)
            {
                // O BitArray 'portMetadataBits' contém os bits da porta na ordem [13, 14, 15]
                for (int indice_port_bit = 0; indice_port_bit < portMetadataBits.Length; indice_port_bit++)
                {
                    // Cada caracter dos 3 primeiros corresponde a um bit, 13=1, 14=2,15=3
                    if (portMetadataBits[indice_port_bit])
                        lastWord = Capitalize(lastWord, indice_port_bit);
                }

                words[words.Count - 1] = lastWord;
            }
        }

        /*
            Para representar os bits extras da porta, usamos os 3 primeiros caracteres da ultima palavra assim>
                000 - nulo - 0 a 8191
                001 - Nulo - 8192 a 16383
                010 - nUlo - 16384 a 24575
                011 - NUlo - 24576 a 32767
                100 - nuLo - 32768 a 40959
                101 - NuLo - 40960 a 49151
                110 - nULo - 49152 a 57343
                111 - NULo - 57344 a 65535
        */
    }

    private (BitArray idBits, BitArray portMetadataBits) ExtractMetadata(string[] words)
    {
        // Extrai os bits do ID diretamente.
        BitArray idBits = new(_config.IdBitCount);

        if (idBits.Length <= words.Length)
        {
            for (int i = 0; i < idBits.Length; i++)
            {
                idBits[i] = char.IsUpper(words[i][0]);
            }
        }

        BitArray portMetadataBits = new(_config.PortMetadataBitCount);
        string lastWord = words.Last();

        if (lastWord.Length < portMetadataBits.Length)
        {
            // O BitArray 'portMetadataBits' contém os bits da porta na ordem [13, 14, 15]
            for (int indice_port_bit = 0; indice_port_bit < portMetadataBits.Length; indice_port_bit++)
            {
                portMetadataBits[indice_port_bit] = char.IsUpper(lastWord[1]);
            }
        }

        return (idBits, portMetadataBits);
    }

    private string Capitalize(string word, int charIndex)
    {
        if (string.IsNullOrEmpty(word) || charIndex < 0 || charIndex >= word.Length)
            return word;

        char[] chars = word.ToCharArray();
        chars[charIndex] = char.ToUpper(chars[charIndex]);
        return new string(chars);
    }
}

public record WordEncoderConfig
{
    // Configuração dos Dados
    public int IpByteCount { get; init; } = 4;      // 32 bits
    public int PortByteCount { get; init; } = 2;     // 16 bits

    // Configuração da Codificação
    public int ChunkBitCount { get; init; } = 9;     // Cada palavra representa 9 bits
    public int IdBitCount { get; init; } = 4;      // 4 bits para o ID do dicionário = 16 dicionários possíveis = 2^4 = 16

    // Configuração dos Metadados
    public int PortMetadataBitCount { get; init; } = 3; // Quantos bits da porta são metadados

    // Propriedades Calculadas
    public int IpBitCount => IpByteCount * 8;
    public int PortBitCount => PortByteCount * 8;
    public int PortDataBitCount => PortBitCount - PortMetadataBitCount;
    public int MainDataBitCount => IpBitCount + PortDataBitCount; // 32 + 13 = 45 bits
    public int ListWordCount => (int)Math.Pow(2, ChunkBitCount); // 2^ChunkBitCount = 2^9 = 512
}

public static class WordListLoader
{
    public static List<string[]> LoadFromDirectory(string path, int requiredWordCount, int Bits_id = 4)
    {
        var max_Id = Math.Pow(2, Bits_id);

        var loadedLists = new List<string[]>();
        for (int id = 0; id < max_Id; id++)
        {
            string filePath = Path.Combine(path, $"Words_{id}.txt");
            if (!File.Exists(filePath))
                break;

            try
            {
                var allLines = File.ReadAllLines(filePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim().ToLower())
                    .ToList();

                if (allLines.Count < requiredWordCount) continue;

                var words = allLines.Take(requiredWordCount).ToArray();
                if (words.Distinct().Count() == requiredWordCount)
                {
                    loadedLists.Add(words);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar '{filePath}': {ex.Message}");
                break;
            }
        }
        return loadedLists;
    }
}

public static class BitArrayUtils
{
    /// <summary>
    /// Inverte a ordem dos bits em um BitArray.
    /// Essencial para corrigir a ordem de maior á esquerda do construtor BitArray(byte[]).
    /// </summary>
    /// <param name="source">O BitArray a ser invertido.</param>
    /// <returns>Um novo BitArray com os bits em ordem reversa.</returns>
    public static BitArray Reverse(BitArray source)
    {
        int length = source.Length;
        BitArray reversed = new(length);

        for (int i = 0; i < length; i++)
            reversed[i] = source[length - 1 - i];

        return reversed;
    }

    /// <summary>
    /// Junta vários BitArrays em um único BitArray contínuo.
    /// Exemplo: [1,0,1] + [0,1] -> [1,0,1,0,1]
    /// </summary>
    /// <param name="arrays">A sequência de BitArrays a serem unidos.</param>
    /// <returns>Um único BitArray contendo todos os bits juntos.</returns>

    public static BitArray Concatenate(params BitArray[] arrays)
    {
        int totalLength = 0;
        foreach (var array in arrays)
            totalLength += array.Length;

        BitArray result = new(totalLength);

        int currentIndex = 0;

        foreach (BitArray array in arrays)
        {
            foreach (bool bit in array)
            {
                result[currentIndex] = bit;
                currentIndex++;
            }
        }

        return result;
    }

    /// <summary>
    /// Separa um BitArray em blocos de tamanho fixo.
    /// Retorna uma lista de blocos completos e, opcionalmente, o último bloco (o resto) se ele for menor.
    /// </summary>
    /// <param name="source">O BitArray de origem.</param>
    /// <param name="chunkSize">O tamanho desejado para cada bloco.</param>
    /// <param name="remainder">O último bloco, se seu tamanho for menor que chunkSize. Caso contrário, null.</param>
    /// <returns>Uma lista de BitArrays, todos com tamanho igual a chunkSize.</returns>
    public static List<BitArray> Split(BitArray source, int chunkSize, out BitArray? remainder)
    {
        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be positive.", nameof(chunkSize));

        List<BitArray> fullChunks = new();
        remainder = null;

        for (int indice_chunk = 0; indice_chunk < source.Length; indice_chunk += chunkSize)
        {
            int remainingInSource = source.Length - indice_chunk;
            int currentChunkLength = Math.Min(chunkSize, remainingInSource);

            BitArray chunk = new(currentChunkLength);

            for (int indice_bit = 0; indice_bit < currentChunkLength; indice_bit++)
                chunk[indice_bit] = source[indice_chunk + indice_bit];

            if (currentChunkLength == chunkSize)
                fullChunks.Add(chunk);
            else
                remainder = chunk;
        }
        return fullChunks;
    }


    /// <summary>
    /// Converte uma lista de BitArrays para uma lista de seus valores decimais.
    /// </summary>
    public static List<int> ToInts(List<BitArray> bitArrays)
    {
        var result = new List<int>();
        foreach (var array in bitArrays)
        {
            result.Add(BitArrayToInt(array));
        }
        return result;
    }

    /// <summary>
    /// Converte um único BitArray para seu valor inteiro.
    /// Lida com números de até 32 bits.
    /// </summary>
    public static int BitArrayToInt(BitArray bitArray)
    {
        if (bitArray.Length > 32)
            throw new ArgumentException("BitArray é demasiado longo para representar um int 32.", nameof(bitArray));

        // Array de bytes para receber os bits. Um int32 tem 4 bytes.
        byte[] bytes = new byte[4];

        // O método CopyTo copia os bits para o array de bytes.
        // Ele lida com o preenchimento e a ordem correta dos bits dentro de cada byte.
        bitArray.CopyTo(bytes, 0);

        // BitConverter.ToInt32 interpreta os 4 bytes como um int,
        return BitConverter.ToInt32(bytes, 0);
    }
}