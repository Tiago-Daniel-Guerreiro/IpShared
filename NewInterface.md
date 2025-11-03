Claro! Transformar uma aplicação de consola numa aplicação com interface gráfica (GUI) usando Avalonia é uma excelente ideia. O padrão MVVM (Model-View-ViewModel) é ideal para isso, pois permite separar a lógica da aplicação (que já tens) da sua apresentação visual.

Abaixo, apresento uma estrutura completa para uma aplicação Avalonia que implementa a funcionalidade do teu código.

### Estrutura do Projeto

Vamos organizar o projeto da seguinte forma:

-   `Views/`
    -   `MainWindow.axaml`: A janela principal com os separadores.
    -   `GenerateView.axaml`: A "página" para gerar convites.
    -   `DecodeView.axaml`: A "página" para descodificar convites.
    -   `HostClientView.axaml`: A "página" para o modo Host/Cliente.
-   `ViewModels/`
    -   `ViewModelBase.cs`: Classe base para todos os ViewModels.
    -   `MainWindowViewModel.cs`: ViewModel principal.
    -   `GenerateViewModel.cs`: Lógica para a `GenerateView`.
    -   `DecodeViewModel.cs`: Lógica para a `DecodeView`.
    -   `HostClientViewModel.cs`: Lógica para a `HostClientView`.
-   `App.axaml.cs`: Ponto de entrada da UI.
-   `Program.cs`: Ponto de entrada da aplicação.

---

### Passo 1: Criar o Projeto Avalonia

1.  Certifica-te que tens os templates do Avalonia instalados:
    ```sh
    dotnet new install Avalonia.Templates
    ```
2.  Cria um novo projeto Avalonia MVVM:
    ```sh
    dotnet new avalonia.mvvm -o InviteGeneratorUI
    cd InviteGeneratorUI
    ```
3.  **Adiciona a tua biblioteca `Invite_Generator.Refactored`** ao projeto. Podes fazer isto adicionando o projeto à solução e referenciando-o, ou compilando-a como uma DLL e adicionando a referência.

---

### Passo 2: Código dos ViewModels

Estes ficheiros contêm a lógica que antes estava no `Program.cs`, mas agora adaptada para interagir com a UI através de propriedades e comandos.

#### `ViewModels/ViewModelBase.cs` (Já vem no template)

```csharp
using ReactiveUI;

namespace InviteGeneratorUI.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
    }
}
```

#### `ViewModels/GenerateViewModel.cs` (Novo Ficheiro)

Este ViewModel gere a lógica de criação de convites.

```csharp
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Invite_Generator.Refactored;
using ReactiveUI;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace InviteGeneratorUI.ViewModels;

public class GenerateViewModel : ViewModelBase
{
    private string _port = "60000";
    public string Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private bool _usePublicIp = true;
    public bool UsePublicIp
    {
        get => _usePublicIp;
        set => this.RaiseAndSetIfChanged(ref _usePublicIp, value);
    }

    private string _statusMessage = "Pronto para gerar convites.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }
    
    // Propriedades para exibir os convites gerados
    private string? _defaultInvite;
    public string? DefaultInvite { get => _defaultInvite; set => this.RaiseAndSetIfChanged(ref _defaultInvite, value); }
    
    private string? _base16Invite;
    public string? Base16Invite { get => _base16Invite; set => this.RaiseAndSetIfChanged(ref _base16Invite, value); }

    private string? _base62Invite;
    public string? Base62Invite { get => _base62Invite; set => this.RaiseAndSetIfChanged(ref _base62Invite, value); }

    private string? _humanInvite;
    public string? HumanInvite { get => _humanInvite; set => this.RaiseAndSetIfChanged(ref _humanInvite, value); }

    private Bitmap? _qrCodeImage;
    public Bitmap? QrCodeImage { get => _qrCodeImage; set => this.RaiseAndSetIfChanged(ref _qrCodeImage, value); }
    
    public async Task GenerateInvitesAsync()
    {
        ClearResults();
        StatusMessage = "A gerar convites, por favor aguarde...";

        if (!ushort.TryParse(Port, out var portNumber))
        {
            StatusMessage = "Erro: A porta inserida é inválida.";
            return;
        }

        try
        {
            InviteGenerator generator;
            if (UsePublicIp)
            {
                StatusMessage = "A obter IP Público via STUN...";
                generator = await InviteGenerator.CreateAsync(portNumber);
            }
            else
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, portNumber);
            }

            // Atualiza as propriedades com os convites
            DefaultInvite = generator.ObterConvite(InviteFormat.Default);
            Base16Invite = generator.ObterConvite(InviteFormat.Base16);
            Base62Invite = generator.ObterConvite(InviteFormat.Base62);

            try { HumanInvite = generator.ObterConvite(InviteFormat.Human); }
            catch (Exception ex) { HumanInvite = $"ERRO: {ex.Message}"; }
            
            // Converte o Base64 do QR Code para uma imagem
            var base64Qr = generator.ObterConvite(InviteFormat.QrCodeBase64);
            var imageBytes = Convert.FromBase64String(base64Qr);
            using (var ms = new MemoryStream(imageBytes))
            {
                QrCodeImage = new Bitmap(ms);
            }
            
            StatusMessage = "Convites gerados com sucesso!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ocorreu um erro: {ex.Message}";
            ClearResults();
        }
    }

    private void ClearResults()
    {
        DefaultInvite = string.Empty;
        Base16Invite = string.Empty;
        Base62Invite = string.Empty;
        HumanInvite = string.Empty;
        QrCodeImage = null;
    }
}
```

#### `ViewModels/DecodeViewModel.cs` (Novo Ficheiro)

Este ViewModel lida com a descodificação de um código de convite.

```csharp
using Invite_Generator.Refactored;
using ReactiveUI;
using System;

namespace InviteGeneratorUI.ViewModels;

public class DecodeViewModel : ViewModelBase
{
    private string? _inviteCode;
    public string? InviteCode
    {
        get => _inviteCode;
        set => this.RaiseAndSetIfChanged(ref _inviteCode, value);
    }

    private string? _resultMessage;
    public string? ResultMessage
    {
        get => _resultMessage;
        set => this.RaiseAndSetIfChanged(ref _resultMessage, value);
    }
    
    private string? _decodedIp;
    public string? DecodedIp { get => _decodedIp; set => this.RaiseAndSetIfChanged(ref _decodedIp, value); }
    
    private string? _decodedPort;
    public string? DecodedPort { get => _decodedPort; set => this.RaiseAndSetIfChanged(ref _decodedPort, value); }
    
    private string? _detectedFormat;
    public string? DetectedFormat { get => _detectedFormat; set => this.RaiseAndSetIfChanged(ref _detectedFormat, value); }

    public void DecodeInvite()
    {
        ClearResults();
        
        if (string.IsNullOrWhiteSpace(InviteCode))
        {
            ResultMessage = "Por favor, insira um código de convite.";
            return;
        }

        var format = InviteGenerator.TryDecodeInvite(InviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            ResultMessage = "Falha ao descodificar: Formato desconhecido ou inválido.";
        }
        else
        {
            ResultMessage = "Convite descodificado com sucesso!";
            DetectedFormat = format.ToString();
            DecodedIp = decodedIpPort.ip.ToString();
            DecodedPort = decodedIpPort.port.ToString();
        }
    }

    private void ClearResults()
    {
        ResultMessage = string.Empty;
        DetectedFormat = string.Empty;
        DecodedIp = string.Empty;
        DecodedPort = string.Empty;
    }
}
```

#### `ViewModels/HostClientViewModel.cs` (Novo Ficheiro)

Este é o mais complexo, pois lida com estado (Host a correr) e tarefas de longa duração (o listener TCP).

```csharp
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Invite_Generator.Refactored;
using ReactiveUI;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InviteGeneratorUI.ViewModels;

public class HostClientViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cts;

    // Propriedades do Host
    private bool _useFixedIp = false;
    public bool UseFixedIp { get => _useFixedIp; set => this.RaiseAndSetIfChanged(ref _useFixedIp, value); }

    private bool _isHostRunning = false;
    public bool IsHostRunning { get => _isHostRunning; set => this.RaiseAndSetIfChanged(ref _isHostRunning, value); }

    private string? _hostInviteCode;
    public string? HostInviteCode { get => _hostInviteCode; set => this.RaiseAndSetIfChanged(ref _hostInviteCode, value); }

    private string _hostLog = "O servidor está parado.";
    public string HostLog { get => _hostLog; set => this.RaiseAndSetIfChanged(ref _hostLog, value); }
    
    // Propriedades do Cliente
    private string? _clientInviteCode;
    public string? ClientInviteCode { get => _clientInviteCode; set => this.RaiseAndSetIfChanged(ref _clientInviteCode, value); }

    private string _clientLog = "Pronto para conectar.";
    public string ClientLog { get => _clientLog; set => this.RaiseAndSetIfChanged(ref _clientLog, value); }

    private bool _isConnecting = false;
    public bool IsConnecting { get => _isConnecting; set => this.RaiseAndSetIfChanged(ref _isConnecting, value); }

    public async Task StartHostAsync()
    {
        if (IsHostRunning) return;
        
        IsHostRunning = true;
        _cts = new CancellationTokenSource();
        const ushort port = 60000;
        HostLog = "[Fase 1/3] A gerar convites...\n";

        try
        {
            InviteGenerator generator;
            if (UseFixedIp)
            {
                generator = InviteGenerator.CreateWithFixedIp(IPAddress.Loopback, port);
                HostLog += "Modo de teste local: Usando IP fixo 127.0.0.1.\n";
            }
            else
            {
                generator = await InviteGenerator.CreateAsync(port);
            }
            
            HostInviteCode = $"Humano: {generator.ObterConvite(InviteFormat.Human)}\n" +
                             $"Default: {generator.ObterConvite(InviteFormat.Default)}";
            
            HostLog += "[Fase 2/3] Convites gerados. Partilhe um código com o cliente.\n";
            HostLog += $"[Fase 3/3] Servidor ativo em 0.0.0.0:{port}. A aguardar conexões...\n";
            
            // Inicia o listener numa thread separada para não bloquear a UI
            _ = Task.Run(() => StartTcpListener(port, _cts.Token));
        }
        catch (Exception ex)
        {
            HostLog += $"Erro Crítico: {ex.Message}\n";
            StopHost();
        }
    }

    public void StopHost()
    {
        if (!IsHostRunning) return;

        _cts?.Cancel();
        IsHostRunning = false;
        HostLog += "Servidor a encerrar...\n";
        HostInviteCode = string.Empty;
    }

    private async Task StartTcpListener(ushort port, CancellationToken token)
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
                        string message = await streamReader.ReadToEndAsync(token);
                        // Para atualizar a UI a partir de outra thread, usamos o Dispatcher
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            HostLog += $"[SERVIDOR] Requisição de {client.Client.RemoteEndPoint}: \"{message.Trim()}\"\n";
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* Ignora, é o encerramento normal */ }
        catch (Exception ex) { await Dispatcher.UIThread.InvokeAsync(() => HostLog += $"Erro no listener: {ex.Message}\n"); }
        finally
        {
            listener.Stop();
            await Dispatcher.UIThread.InvokeAsync(() => HostLog += "Servidor encerrado.\n");
        }
    }

    public async Task ConnectClientAsync()
    {
        if (IsConnecting) return;
        
        IsConnecting = true;
        ClientLog = "A iniciar conexão...\n";

        if (string.IsNullOrWhiteSpace(ClientInviteCode))
        {
            ClientLog += "Código de convite inválido.\n";
            IsConnecting = false;
            return;
        }

        var format = InviteGenerator.TryDecodeInvite(ClientInviteCode, out var decodedIpPort);

        if (format == InviteFormat.Unknown)
        {
            ClientLog += "Falha ao descodificar o código.\n";
            IsConnecting = false;
            return;
        }

        ClientLog += $"Código descodificado. A tentar conectar a {decodedIpPort.ip}:{decodedIpPort.port}...\n";

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(decodedIpPort.ip, decodedIpPort.port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
            {
                throw new TimeoutException("A tentativa de conexão excedeu o tempo limite.");
            }

            ClientLog += "Conexão bem-sucedida!\n";

            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            string message = $"Olá do cliente! O código '{format}' funcionou.";
            await writer.WriteAsync(message);
            ClientLog += $"Mensagem enviada: \"{message}\"\n";
        }
        catch (Exception ex)
        {
            ClientLog += $"Falha ao conectar: {ex.Message}\n";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

#### `ViewModels/MainWindowViewModel.cs`

Este ViewModel agrega os outros.

```csharp
namespace InviteGeneratorUI.ViewModels;

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
```

---

### Passo 3: Código das Views (AXAML)

Estes ficheiros definem a aparência da aplicação.

#### `Views/GenerateView.axaml` (Novo Ficheiro)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:InviteGeneratorUI.ViewModels"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
             x:Class="InviteGeneratorUI.Views.GenerateView"
             x:DataType="vm:GenerateViewModel">
    <Design.DataContext>
        <vm:GenerateViewModel />
    </Design.DataContext>

    <Grid RowDefinitions="Auto,*,Auto" Margin="15">
        <!-- Secção de Controlo -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Gerar Convites a partir de um IP" FontSize="18" FontWeight="Bold"/>
            <WrapPanel Spacing="10" VerticalAlignment="Center">
                <TextBlock Text="Porta:" VerticalAlignment="Center"/>
                <TextBox Text="{Binding Port}" Width="80"/>
                <CheckBox Content="Usar IP Público (STUN)" IsChecked="{Binding UsePublicIp}"/>
                <Button Content="Gerar Convites" Command="{Binding GenerateInvitesAsync}" Classes="accent"/>
            </WrapPanel>
        </StackPanel>

        <!-- Secção de Resultados -->
        <ScrollViewer Grid.Row="1" Margin="0,15,0,0">
            <StackPanel Spacing="15">
                <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto" IsVisible="{Binding DefaultInvite, Converter={x:Static StringConverters.IsNotNullOrEmpty}}">
                    <TextBlock Grid.Row="0" Grid.Column="0" Text="Default:" FontWeight="Bold" Margin="0,0,10,0"/>
                    <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding DefaultInvite}" IsReadOnly="True"/>

                    <TextBlock Grid.Row="1" Grid.Column="0" Text="Base16 (Hex):" FontWeight="Bold" Margin="0,0,10,0"/>
                    <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding Base16Invite}" IsReadOnly="True"/>
                    
                    <TextBlock Grid.Row="2" Grid.Column="0" Text="Base62:" FontWeight="Bold" Margin="0,0,10,0"/>
                    <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding Base62Invite}" IsReadOnly="True"/>

                    <TextBlock Grid.Row="3" Grid.Column="0" Text="Formato Humano:" FontWeight="Bold" Margin="0,0,10,0"/>
                    <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding HumanInvite}" IsReadOnly="True"/>
                </Grid>

                <StackPanel Orientation="Horizontal" Spacing="10" IsVisible="{Binding QrCodeImage, Converter={x:Static ObjectConverters.IsNotNull}}">
                     <TextBlock Text="QR Code:" FontWeight="Bold" VerticalAlignment="Center"/>
                     <Image Source="{Binding QrCodeImage}" Width="150" Height="150" Margin="10"/>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>

        <!-- Barra de Status -->
        <Border Grid.Row="2" Background="{DynamicResource SystemControlPageBackgroundAltHighBrush}" CornerRadius="4" Padding="8">
            <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center"/>
        </Border>
    </Grid>
</UserControl>
```

Cria o ficheiro C# correspondente `Views/GenerateView.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace InviteGeneratorUI.Views;

public partial class GenerateView : UserControl
{
    public GenerateView()
    {
        InitializeComponent();
    }
}
```

#### `Views/DecodeView.axaml` (Novo Ficheiro)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:InviteGeneratorUI.ViewModels"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
             x:Class="InviteGeneratorUI.Views.DecodeView"
             x:DataType="vm:DecodeViewModel">
    <Grid RowDefinitions="Auto,*,Auto" Margin="15">
        <!-- Controlo -->
        <StackPanel Grid.Row="0" Spacing="10">
            <TextBlock Text="Obter IP a partir de um Convite" FontSize="18" FontWeight="Bold"/>
            <TextBlock Text="Cole o código de convite (ou o conteúdo Base64 do QR Code):"/>
            <TextBox Text="{Binding InviteCode}" Watermark="Insira o código aqui..."/>
            <Button Content="Descodificar" Command="{Binding DecodeInvite}" Classes="accent"/>
        </StackPanel>

        <!-- Resultados -->
        <Border Grid.Row="1" Margin="0,20,0,0" Padding="15" CornerRadius="5"
                Background="{DynamicResource SystemControlPageBackgroundListLowBrush}"
                IsVisible="{Binding DetectedFormat, Converter={x:Static StringConverters.IsNotNullOrEmpty}}">
            <StackPanel Spacing="10">
                 <Grid ColumnDefinitions="150, *">
                    <TextBlock Grid.Column="0" Text="Formato Detetado:" FontWeight="Bold"/>
                    <TextBlock Grid.Column="1" Text="{Binding DetectedFormat}"/>
                </Grid>
                 <Grid ColumnDefinitions="150, *">
                    <TextBlock Grid.Column="0" Text="Endereço IP:" FontWeight="Bold"/>
                    <TextBlock Grid.Column="1" Text="{Binding DecodedIp}"/>
                </Grid>
                <Grid ColumnDefinitions="150, *">
                    <TextBlock Grid.Column="0" Text="Porta:" FontWeight="Bold"/>
                    <TextBlock Grid.Column="1" Text="{Binding DecodedPort}"/>
                </Grid>
            </StackPanel>
        </Border>

        <!-- Status -->
        <Border Grid.Row="2" Background="{DynamicResource SystemControlPageBackgroundAltHighBrush}" CornerRadius="4" Padding="8">
             <TextBlock Text="{Binding ResultMessage}" VerticalAlignment="Center"/>
        </Border>
    </Grid>
</UserControl>
```

Ficheiro `Views/DecodeView.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace InviteGeneratorUI.Views;

public partial class DecodeView : UserControl
{
    public DecodeView()
    {
        InitializeComponent();
    }
}
```

#### `Views/HostClientView.axaml` (Novo Ficheiro)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:vm="using:InviteGeneratorUI.ViewModels"
             mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="600"
             x:Class="InviteGeneratorUI.Views.HostClientView"
             x:DataType="vm:HostClientViewModel">
    <TabControl Margin="15">
        <!-- MODO HOST -->
        <TabItem Header="Hospedar Sessão (Host)">
            <Grid RowDefinitions="Auto, *, Auto" Margin="10">
                <StackPanel Grid.Row="0" Spacing="10">
                    <WrapPanel Spacing="10" VerticalAlignment="Center">
                        <Button Content="Iniciar Host" Command="{Binding StartHostAsync}" IsEnabled="{Binding !IsHostRunning}" Classes="accent"/>
                        <Button Content="Parar Host" Command="{Binding StopHost}" IsEnabled="{Binding IsHostRunning}"/>
                        <CheckBox Content="Usar IP de Teste (127.0.0.1)" IsChecked="{Binding UseFixedIp}" IsEnabled="{Binding !IsHostRunning}"/>
                    </WrapPanel>
                    <TextBox Text="{Binding HostInviteCode}" TextWrapping="Wrap" IsReadOnly="True" Height="80" Watermark="Os convites aparecerão aqui..."/>
                </StackPanel>

                <ScrollViewer Grid.Row="1" Margin="0,10,0,0">
                    <TextBox Text="{Binding HostLog}" AcceptsReturn="True" TextWrapping="Wrap" IsReadOnly="True" />
                </ScrollViewer>
            </Grid>
        </TabItem>
        
        <!-- MODO CLIENTE -->
        <TabItem Header="Conectar a Sessão (Cliente)">
            <Grid RowDefinitions="Auto, *, Auto" Margin="10">
                <StackPanel Grid.Row="0" Spacing="10">
                     <TextBox Text="{Binding ClientInviteCode}" Watermark="Cole o código do convite aqui"/>
                     <Button Content="Conectar" Command="{Binding ConnectClientAsync}" IsEnabled="{Binding !IsConnecting}" Classes="accent"/>
                </StackPanel>
                
                <ScrollViewer Grid.Row="1" Margin="0,10,0,0">
                    <TextBox Text="{Binding ClientLog}" AcceptsReturn="True" TextWrapping="Wrap" IsReadOnly="True" />
                </ScrollViewer>
            </Grid>
        </TabItem>
    </TabControl>
</UserControl>
```

Ficheiro `Views/HostClientView.axaml.cs`:
```csharp
using Avalonia.Controls;

namespace InviteGeneratorUI.Views;

public partial class HostClientView : UserControl
{
    public HostClientView()
    {
        InitializeComponent();
    }
}
```

#### `Views/MainWindow.axaml` (Modificar o existente)

Substitui o conteúdo do `MainWindow.axaml` pelo seguinte, que usa um `TabControl` para organizar as diferentes funcionalidades.

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:InviteGeneratorUI.ViewModels"
        xmlns:views="using:InviteGeneratorUI.Views"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="600"
        x:Class="InviteGeneratorUI.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Icon="/Assets/avalonia-logo.ico"
        Title="Utilitário de Convites P2P"
        Width="800" Height="600">

    <Design.DataContext>
        <vm:MainWindowViewModel/>
    </Design.DataContext>
    
    <TabControl>
        <TabItem Header="Gerar Convite">
            <views:GenerateView DataContext="{Binding GenerateVM}"/>
        </TabItem>
        <TabItem Header="Descodificar Convite">
            <views:DecodeView DataContext="{Binding DecodeVM}"/>
        </TabItem>
        <TabItem Header="Teste de Conexão">
            <views:HostClientView DataContext="{Binding HostClientVM}"/>
        </TabItem>
    </TabControl>

</Window>
```

---

### Passo 4: Conectar Tudo

#### `App.axaml.cs`

Verifica se este ficheiro está a instanciar o `MainWindowViewModel` e a passá-lo para a `MainWindow`.

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InviteGeneratorUI.ViewModels;
using InviteGeneratorUI.Views;

namespace InviteGeneratorUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            // Garante que o recurso CancellationTokenSource é libertado ao fechar
            desktop.ShutdownRequested += (sender, args) =>
            {
                mainViewModel.HostClientVM.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

Agora, ao executares o projeto, terás uma aplicação de desktop funcional com três separadores, replicando e melhorando a usabilidade da tua aplicação de consola original.