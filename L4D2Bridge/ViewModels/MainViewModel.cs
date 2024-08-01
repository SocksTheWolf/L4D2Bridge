using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using L4D2Bridge.Models;
using System;
using System.Collections.ObjectModel;

namespace L4D2Bridge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConfigData Config { get; private set; }
    public ConsoleService Console { get; set; } = new ConsoleService();
    private RCONService Server { get; set; }
    private TiltifyService? Donation { get; set; }
    private Button? PauseButton { get; set; }

    [ObservableProperty]
    public string pauseButtonText = string.Empty;

    [ObservableProperty]
    public bool isPaused = false;

    [ObservableProperty]
    public string pauseTip = "Click here to pause the server";

    public MainViewModel() 
    {
        SetPauseGlyph("f04b");
        Config = ConfigData.LoadConfigData();
        Console.Initialize(Config);
        Server = new RCONService(Config);

        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, EConsoleSource.RCON);
        Server.OnPauseStatus = OnPauseStatusUpdate;
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
    private void OnPauseStatusUpdate(bool CurrentPauseStatus)
    {
        if (PauseButton == null)
            return;

        IsPaused = CurrentPauseStatus;
        if (CurrentPauseStatus)
        {
            PauseButton.Foreground = Brushes.Orange;
            SetPauseGlyph("f04c");
            PauseTip = "Server is currently paused. Click to unpause.";
        }
        else
        {
            PauseButton.Foreground = Brushes.Green;
            PauseTip = "Click here to pause the server";
            SetPauseGlyph("f04b");
        }
    }

    private void SetPauseGlyph(string BaseGlyph)
    {
        var chars = new char[] { (char)Convert.ToInt32(BaseGlyph, 16) };
        PauseButtonText = new string(chars);
    }

    // Button to allow for pausing outside of the game
    public void OnPauseButton_Clicked(object msg)
    {
        ReadOnlyCollection<object> Payload = (ReadOnlyCollection<object>)msg;
        PauseButton = ((Button)Payload[0]);

        Server.AddNewCommand(new TogglePauseCommand());
    }

    public void OnServerCommand_Sent(object msg)
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
    // - Rules engine system thing yeah
}
