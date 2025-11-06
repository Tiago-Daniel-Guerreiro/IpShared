using System.Threading.Tasks;

namespace IpShared.Platform;

public interface IPlatformScanner
{
    /// <summary>
    /// Abre a interface de scan (ou câmera) e tenta decodificar um QR Code, retornando o texto lido ou null.
    /// </summary>
    Task<string?> ScanAsync();
}
