namespace IpWordEncoder;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Define os modos de codificação possíveis.
/// </summary>
public enum EncodingMode
{
    IpAndPort_Interleaved,
    IpOnly_EmbeddedId
}

/// <summary>
/// Configuração imutável para os algoritmos de codificação.
/// </summary>
public record WordEncoderConfig
{
    public int ChunkBitCount { get; init; } = 9;
    public int IdBitCount { get; init; } = 4;
    public int PortMetadataBitCount { get; init; } = 3;
    public int DictionaryWordCount => 1 << ChunkBitCount; // dicionariy word count =  ChunkBitCount = 9, 9 bits = 512
    public int PortDataBitCount => 16 - PortMetadataBitCount; // Quantidade de bits da porta = 16, - bits representados com maisuclas
}

/// <summary>
/// Nova classe para encapsular a configuração dos testes.
/// </summary>
public record TestRunConfig(int NumberOfRandomTests, bool RunMinMaxTests);

/// <summary>
/// Estrutura de dados unificada para passar dados para as estratégias de codificação.
/// </summary>
public record EncodingData(IPAddress Ip, ushort? Port, int ListId);

/// <summary>
/// Estrutura para devolver o resultado de uma série de testes.
/// </summary>
public record TestResult(int Errors, int TotalTests);

/// <summary>
/// Interface comum para todas as estratégias de codificação.
/// </summary>
public interface IWordEncodingStrategy
{
    string Encode(EncodingData data);
    EncodingData Decode(string encoded);
}

/// <summary>
/// Implementação da estratégia para codificar IP e Porta.
/// Inverte os bits de metadados da porta para a capitalização,
/// tornando o efeito mais legível (ex: 001 -> 100 -> Pote).
/// 5 bits menos significativos afetam o bit mais significativo de cada conjunto de 8 bits, para maior variadade.
/// </summary>
public class IpAndPortEncodingStrategy : IWordEncodingStrategy
{
    private const int IP_BYTE_COUNT = 4;
    private const int BITS_PER_BYTE = 8;

    private const int PORT_DATA_HIGH_BIT_COUNT = 8;
    private const int PORT_DATA_LOW_BIT_COUNT = 5;

    private readonly WordEncoderConfig _config;
    private readonly IReadOnlyList<string[]> _wordLists;
    private readonly IReadOnlyList<Dictionary<string, int>> _wordMaps;

    public IpAndPortEncodingStrategy(WordEncoderConfig config, IReadOnlyList<string[]> wordLists, IReadOnlyList<Dictionary<string, int>> wordMaps)
    {
        _config = config;
        _wordLists = wordLists;
        _wordMaps = wordMaps;
    }

    public string Encode(EncodingData data)
    {
        if (!data.Port.HasValue)
            throw new ArgumentException("A porta é obrigatória para esta estratégia de codificação.", nameof(data));

        List<BitArray> finalChunks = InterleaveIpAndPort(data.Ip, data.Port.Value);
        List<int> indices = finalChunks.Select(BitArrayUtils.ToInt).ToList();
        List<string> outputWords = ConvertIndicesToWords(indices, data.ListId);

        ApplyMetadata(outputWords, data.ListId, data.Port.Value);

        return string.Join("-", outputWords);
    }

    public EncodingData Decode(string encoded)
    {
        string[] words = encoded.Split('-');
        if (words.Length != 5)
            throw new ArgumentException("A frase codificada deve conter 5 palavras.", nameof(encoded));

        var (listId, portMetadataBits) = ExtractMetadataFromWords(words);
        var wordMap = _wordMaps[listId];

        List<BitArray> finalChunks = words.Select(word => BitArrayUtils.FromInt(GetWordIndex(word, wordMap), _config.ChunkBitCount)).ToList();

        var (ipBits, portDataBits) = DeinterleaveIpAndPort(finalChunks);
        IPAddress decodedIp = new(ipBits.ToBigEndianBytes());
        ushort decodedPort = ReconstructPort(portDataBits, portMetadataBits);

        return new EncodingData(decodedIp, decodedPort, listId);
    }

    private void ApplyMetadata(List<string> words, int listId, ushort port)
    {
        // A lógica do ID não muda
        BitArray idBits = new([listId]);

        for (int i = 0; i < _config.IdBitCount && i < words.Count; i++)
        {
            if (idBits[i]) words[i] = Capitalize(words[i], 0);
        }

        BitArray portBits = BitArrayUtils.FromInt(port, 16);
        BitArray originalMetadataBits = portBits.GetRange(0, _config.PortMetadataBitCount);

        // Inverter a ordem dos bits de metadados para a aplicação. Ex: 001 (original) -> 100 (para aplicar)

        BitArray reversedMetadataBits = BitArrayUtils.Reverse(originalMetadataBits);

        if (reversedMetadataBits.Length > 0 && words.Count > 0)
        {
            string lastWord = words.Last();
            char[] chars = lastWord.ToCharArray();
            // Usa os bits invertidos para a capitalização
            for (int i = 0; i < reversedMetadataBits.Length && i < chars.Length; i++)
            {
                if (reversedMetadataBits[i])
                {
                    chars[i] = char.ToUpper(chars[i]);
                }
            }
            words[words.Count - 1] = new string(chars);
        }
    }

    private (int listId, BitArray portMetadata) ExtractMetadataFromWords(string[] words)
    {
        // A extração do ID não muda
        BitArray idBits = new(_config.IdBitCount);

        for (int i = 0; i < idBits.Length && i < words.Length; i++)
        {
            idBits[i] = char.IsUpper(words[i][0]);
        }

        int[] idTemp = new int[1];

        idBits.CopyTo(idTemp, 0);

        int listId = idTemp[0];

        if (listId < 0 || listId >= _wordMaps.Count)
            throw new KeyNotFoundException($"ID da lista inválido: {listId}.");

        // Extrai o padrão de capitalização como ele aparece na palavra. Ex: "Pote" -> 100
        BitArray capitalizationPattern = new(_config.PortMetadataBitCount);
        if (words.Length > 0)
        {
            string lastWord = words.Last();
            for (int i = 0; i < capitalizationPattern.Length && i < lastWord.Length; i++)
            {
                capitalizationPattern[i] = char.IsUpper(lastWord[i]);
            }
        }

        // Inverte o padrão extraído para obter os bits de metadados originais.
        // Ex: 100 (de "Pote") -> 001 (bits originais da porta)
        BitArray originalMetadataBits = BitArrayUtils.Reverse(capitalizationPattern);

        return (listId, originalMetadataBits);
    }

    private List<BitArray> InterleaveIpAndPort(IPAddress ip, ushort port)
    {
        BitArray ipBits = BitArrayUtils.FromBigEndianBytes(ip.GetAddressBytes());
        List<BitArray> ipChunks = ipBits.Split(BITS_PER_BYTE);

        BitArray portBits = BitArrayUtils.FromInt(port, 16);
        BitArray portDataHigh8 = portBits.GetRange(_config.PortMetadataBitCount, PORT_DATA_HIGH_BIT_COUNT);
        BitArray portDataLow5 = portBits.GetRange(_config.PortMetadataBitCount + PORT_DATA_HIGH_BIT_COUNT, PORT_DATA_LOW_BIT_COUNT);

        List<BitArray> finalChunks = new(5);

        for (int i = 0; i < IP_BYTE_COUNT; i++)
        {
            finalChunks.Add(BitArrayUtils.Concatenate(new BitArray(1, portDataLow5[i]), ipChunks[i]));
        }

        finalChunks.Add(BitArrayUtils.Concatenate(new BitArray(1, portDataLow5[4]), portDataHigh8));

        return finalChunks;
    }

    private (BitArray ipBits, BitArray portDataBits) DeinterleaveIpAndPort(List<BitArray> finalChunks)
    {
        BitArray[] ipChunks = new BitArray[IP_BYTE_COUNT];
        BitArray portDataLow5 = new(PORT_DATA_LOW_BIT_COUNT);

        for (int i = 0; i < IP_BYTE_COUNT; i++)
        {
            portDataLow5[i] = finalChunks[i][0];
            ipChunks[i] = finalChunks[i].GetRange(1, BITS_PER_BYTE);
        }

        portDataLow5[4] = finalChunks[4][0];
        BitArray portDataHigh8 = finalChunks[4].GetRange(1, PORT_DATA_HIGH_BIT_COUNT);

        BitArray ipBits = BitArrayUtils.Concatenate(ipChunks);
        BitArray portDataBits = BitArrayUtils.Concatenate(portDataHigh8, portDataLow5);

        return (ipBits, portDataBits);
    }

    private ushort ReconstructPort(BitArray portDataBits, BitArray portMetadataBits)
    {
        BitArray fullPortBits = BitArrayUtils.Concatenate(portMetadataBits, portDataBits);
        return (ushort)fullPortBits.ToInt();
    }

    private List<string> ConvertIndicesToWords(List<int> indices, int listId)
    {
        IReadOnlyList<string> wordList = _wordLists[listId];
        return indices.Select(index => wordList[index]).ToList();
    }

    private int GetWordIndex(string word, IReadOnlyDictionary<string, int> map)
    {
        string lowerWord = word.ToLowerInvariant();
        if (!map.TryGetValue(lowerWord, out int index))
            throw new KeyNotFoundException($"A palavra '{lowerWord}' não foi encontrada no dicionário.");

        return index;
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

/// <summary>
/// Implementação da estratégia para codificar apenas o IP.
/// </summary>
public class IpOnlyEncodingStrategy : IWordEncodingStrategy
{
    private readonly IReadOnlyList<string[]> _wordLists;
    private readonly IReadOnlyList<Dictionary<string, int>> _wordMaps;

    public IpOnlyEncodingStrategy(IReadOnlyList<string[]> wordLists, IReadOnlyList<Dictionary<string, int>> wordMaps)
    {
        _wordLists = wordLists;
        _wordMaps = wordMaps;
    }

    public string Encode(EncodingData data)
    {
        if (data.Port.HasValue)
            throw new ArgumentException("A porta não é suportada para esta estratégia de codificação.", nameof(data));

        byte[] ipBytes = data.Ip.GetAddressBytes();
        List<int> indices = ipBytes.Select(b => (int)b).ToList();

        indices.Add(data.ListId);

        IReadOnlyList<string> wordList = _wordLists[data.ListId];
        List<string> outputWords = indices.Select(index => wordList[index]).ToList();

        return string.Join("-", outputWords);
    }

    public EncodingData Decode(string encoded)
    {
        string[] words = encoded.Split('-');
        if (words.Length != 5)
            throw new ArgumentException("A frase codificada deve conter 5 palavras.", nameof(encoded));

        for (int listId = 0; listId < _wordMaps.Count; listId++)
        {
            var wordMap = _wordMaps[listId];
            string idWord = words[4];

            if (wordMap.TryGetValue(idWord, out int decodedId) && decodedId == listId)
            {
                try
                {
                    List<int> ipIndices = words.Take(4).Select(word => wordMap[word]).ToList();
                    byte[] ipBytes = ipIndices.Select(i => (byte)i).ToArray();

                    if (ipIndices.All(i => i >= 0 && i <= 255))
                        return new EncodingData(new IPAddress(ipBytes), null, listId);
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }
            }
        }

        throw new Exception("Não foi possível descodificar a frase com nenhum dos dicionários disponíveis.");
    }
}

public class WordEncoder
{
    private readonly IWordEncodingStrategy _strategy;

    public WordEncoder(IWordEncodingStrategy strategy)
    {
        _strategy = strategy;
    }

    public string Encode(IPAddress ip, ushort port, int listId = 0)
    {
        EncodingData data = new(ip, port, listId);
        return _strategy.Encode(data);
    }

    public string Encode(IPAddress ip, int listId = 0)
    {
        EncodingData data = new(ip, null, listId);
        return _strategy.Encode(data);
    }

    public (IPAddress ip, ushort? port, int id) Decode(string encoded)
    {
        EncodingData result = _strategy.Decode(encoded);
        return (result.Ip, result.Port, result.ListId);
    }
}

public static class WordListLoader
{
    public static List<string[]> LoadFromDirectory(string path, int requiredWordCount, int idBitCount = 4)
    {
        List<string[]> loadedLists = new();
        int maxId = 1 << idBitCount;

        for (int id = 0; id < maxId; id++)
        {
            // Tenta carregar do recurso embutido primeiro
            string[]? words = LoadFromEmbeddedResource(id, requiredWordCount);
            
            if (words != null)
            {
                loadedLists.Add(words);
                continue;
            }

            // Fallback: tenta carregar do sistema de arquivos
            string filePath = Path.Combine(path, $"Words_{id}.txt");
            if (!File.Exists(filePath))
            {
                if (id > 0) Console.WriteLine($"Aviso: Ficheiro 'Words_{id}.txt' não encontrado. A procura terminou.");
                break;
            }
            try
            {
                words = File.ReadAllLines(filePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim().ToLowerInvariant())
                    .Take(requiredWordCount)
                    .ToArray();

                if (words.Length == requiredWordCount && words.Distinct(StringComparer.InvariantCultureIgnoreCase).Count() == words.Length)
                    loadedLists.Add(words);
                else
                    Console.WriteLine($"Aviso: O ficheiro '{filePath}' não tem {requiredWordCount} palavras únicas ou tem duplicados. Foi ignorado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar '{filePath}': {ex.Message}");
            }
        }
        return loadedLists;
    }

    private static string[]? LoadFromEmbeddedResource(int id, int requiredWordCount)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // Alguns targets (ex: Android) podem ter nomes de recursos diferentes (namespace alterado pelo linker/projeto).
            // Procuramos qualquer resource cujo nome termine com 'Words_{id}.txt' (case-insensitive) para maior robustez.
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($"Words_{id}.txt", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                return null;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            var words = new List<string>();
            
            string? line;
            while ((line = reader.ReadLine()) != null && words.Count < requiredWordCount)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    words.Add(line.Trim().ToLowerInvariant());
            }

            if (words.Count == requiredWordCount && words.Distinct(StringComparer.InvariantCultureIgnoreCase).Count() == words.Count)
                return words.ToArray();
                
            return null;
        }
        catch
        {
            return null;
        }
    }
}

public static class BitArrayUtils
{
    public static BitArray FromBigEndianBytes(byte[] bytes)
    {
        var bits = new BitArray(bytes.Length * 8);
        for (int i = 0; i < bytes.Length; i++)
        {
            for (int j = 0; j < 8; j++)
                bits[i * 8 + j] = (bytes[i] & (1 << (7 - j))) != 0;
        }
        return bits;
    }

    public static byte[] ToBigEndianBytes(this BitArray bits)
    {
        if (bits.Length % 8 != 0)
            throw new ArgumentException("O comprimento do BitArray deve ser um múltiplo de 8.");

        var bytes = new byte[bits.Length / 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte currentByte = 0;
            for (int j = 0; j < 8; j++)
                if (bits[i * 8 + j])
                    currentByte |= (byte)(1 << (7 - j));
            bytes[i] = currentByte;
        }
        return bytes;
    }

    public static BitArray FromInt(int value, int length)
    {
        var bits = new BitArray(length);
        for (int i = 0; i < length; i++)
            bits[i] = (value & (1 << (length - 1 - i))) != 0;
        return bits;
    }

    public static int ToInt(this BitArray bitArray)
    {
        if (bitArray.Length > 32)
            throw new ArgumentException("BitArray demasiado longo.", nameof(bitArray));

        int value = 0;
        for (int i = 0; i < bitArray.Length; i++)
            if (bitArray[i])
                value |= 1 << (bitArray.Length - 1 - i);
        return value;
    }

    public static BitArray Concatenate(params BitArray[] arrays)
    {
        int totalLength = arrays.Sum(a => a.Length);
        var result = new BitArray(totalLength);
        int currentIndex = 0;
        foreach (var array in arrays)
        {
            for (int i = 0; i < array.Length; i++)
                result[currentIndex++] = array[i];
        }
        return result;
    }

    public static List<BitArray> Split(this BitArray source, int chunkSize)
    {
        var chunks = new List<BitArray>((source.Length + chunkSize - 1) / chunkSize);
        for (int i = 0; i < source.Length; i += chunkSize)
        {
            int size = Math.Min(chunkSize, source.Length - i);
            var chunk = new BitArray(size);
            for (int j = 0; j < size; j++)
                chunk[j] = source[i + j];
            chunks.Add(chunk);
        }
        return chunks;
    }

    public static BitArray GetRange(this BitArray source, int index, int count)
    {
        if (index < 0 || count < 0 || index + count > source.Length)
            throw new ArgumentOutOfRangeException();

        var range = new BitArray(count);
        for (int i = 0; i < count; i++)
            range[i] = source[index + i];
        return range;
    }
    /// <summary>
    /// Inverte a ordem dos bits em um BitArray.
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
}