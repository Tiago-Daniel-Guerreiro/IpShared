using System.Threading.Tasks;

namespace IpShared.Platform
{
    public interface IPlatformTextEditor
    {
        /// <summary>
        /// Abre um editor de texto nativo na plataforma e retorna o texto editado (ou null se cancelado).
        /// </summary>
        Task<string?> EditTextAsync(string initialText, bool readOnly = false);
    }
}
