using System.Threading.Tasks;
using IpShared.Platform;
using Android.App;

namespace IpShared.Android;

public class AndroidPlatformScanner : IPlatformScanner
{
    public Task<string?> ScanAsync()
    {
        // Delegar para MainActivity que tem a lógica para iniciar a câmera e decodificar
        return MainActivity.StartCameraAndDecodeAsync();
    }
}
