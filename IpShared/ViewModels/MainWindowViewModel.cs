namespace IpShared.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public GenerateViewModel GenerateVM { get; }
    public DecodeViewModel DecodeVM { get; }
    public HostClientViewModel HostClientVM { get; }

    public MainWindowViewModel()
    {
        GenerateVM = new GenerateViewModel();
        DecodeVM = new DecodeViewModel();
        HostClientVM = new HostClientViewModel();
    }
}
