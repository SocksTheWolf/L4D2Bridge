using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using L4D2Tiltify.Models;
using System.Collections.ObjectModel;

namespace L4D2Tiltify.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConfigData Config { get; private set; }
    public ConsoleService Console { get; set; } = new ConsoleService();
    private RCONService Server { get; set; }
    private TiltifyService? Donation { get; set; }

    [ObservableProperty]
    public bool isPaused = false;

    public MainViewModel() 
    { 
        Config = ConfigData.LoadConfigData();
        Console.Initialize(Config);
        Server = new RCONService(Config);

        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, EConsoleSource.RCON);
        Server.OnPauseStatus = OnPauseStatus;
        Server.Start();

        if (Config.UseTiltify)
        {
            Donation = new TiltifyService(Config);
            Donation.OnConsolePrint = (msg) => Console.AddMessage(msg, EConsoleSource.Tiltify);
            Donation.OnDonationReceived = (data) =>
            {
                Console.AddMessage($"{data.Name} donated {data.Amount} {data.Currency}", EConsoleSource.Tiltify);
            };
            Donation.OnAuthUpdate = (data) =>
            {
                Config.TiltifyOAuthToken = data.OAuthToken;
                Config.TiltifyRefreshToken = data.RefreshToken;
                Config.SaveConfigData();
                Console.AddMessage("OAuth Data Updated!", EConsoleSource.Tiltify);
            };
            Donation.Start();
        }

        Config.SaveConfigData();
        Console.AddMessage("Operations Running!", EConsoleSource.Main);
    }

    // Flags our UI if the status of the server is paused
    private void OnPauseStatus(bool CurrentPauseStatus)
    {
        IsPaused = CurrentPauseStatus;
    }

    // Button to allow for pausing outside of the game
    public void PauseButton(object msg)
    {
        Server.AddNewCommand(new TogglePauseCommand());
    }

    public void PushServerCommand(object msg)
    {        
        ReadOnlyCollection<object> Payload = (ReadOnlyCollection<object>)msg;
        TextBox Box = ((TextBox)Payload[0]);
        if (!string.IsNullOrEmpty(Box.Text))
        {
            Server.AddNewCommand(new RawCommand(Box.Text));
            Box.Clear();
        }
    }

    // TODO:
    //
    // - Pause Status
    // - Pause Widget
    // - Rules engine system thing yeah
    // - Ability to update the Tiltify API Key during runtime
}
