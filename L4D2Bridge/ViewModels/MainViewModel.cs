using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using L4D2Bridge.Models;
using L4D2Bridge.Types;
using System;
using System.Collections.Generic;

namespace L4D2Bridge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConfigData Config { get; private set; }
    public ConsoleService Console { get; set; } = new ConsoleService();
    private RCONService Server { get; set; }
    private RulesService Rules { get; set; }
    private TiltifyService? CharityTracker { get; set; }
    private Button? PauseButton { get; set; }

    [ObservableProperty]
    public string pauseButtonText = string.Empty;

    [ObservableProperty]
    public bool isPaused = false;

    [ObservableProperty]
    public string pauseTip = "Click here to pause the server";

    public MainViewModel() 
    {
        Config = ConfigData.LoadConfigData();

        // Push the mob size prefs to the command builder
        L4D2CommandBuilder.Mobs = Config.MobSizes;

        // Start the console service
        Console.Start();
        SetPauseGlyph("f04b");

        /* RCON */
        Server = new RCONService(Config);
        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, Server);
        Server.OnPauseStatus = OnPauseStatusUpdate;
        Server.Start();

        /* Rules Engine */
        Rules = new RulesService(ref Config.Actions);
        Rules.OnConsolePrint = (msg) => Console.AddMessage(msg, Rules);
        Rules.Start();

        /* Tiltify */
        if (Config.TiltifySettings != null && Config.TiltifySettings.Enabled)
        {
            CharityTracker = new TiltifyService(Config.TiltifySettings);
            CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, CharityTracker);
            CharityTracker.OnDonationReceived = async (data) =>
            {
                Console.AddMessage($"{data.Name} donated {data.Amount}", CharityTracker);
                List<L4D2Action> Commands = await Rules.ExecuteAsync(CharityTracker.GetWorkflow(), data);
                Server.AddNewActions(Commands, data.Name);
            };
            CharityTracker.OnAuthUpdate = (data) =>
            {
                Config.TiltifySettings.OAuthToken = data.OAuthToken;
                if (!string.IsNullOrEmpty(data.RefreshToken))
                    Config.TiltifySettings.RefreshToken = data.RefreshToken;
                Config.SaveConfigData();
                Console.AddMessage("OAuth Data Updated!", CharityTracker);
            };
            CharityTracker.Start();
        }

        Config.SaveConfigData();
        Console.AddMessage("Operations Running!", ConsoleSources.Main);
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
        if (PauseButton == null)
            PauseButton = ((Button)msg);

        Server.AddNewCommand(new TogglePauseCommand());
    }

    public void OnServerCommand_Sent(object msg)
    {
        TextBox Box = ((TextBox)msg);
        string? command = Box.Text;
        if (!string.IsNullOrEmpty(command))
        {
            if (command.ToLower() == "clear")
                Console.ClearAllMessages();
            else
                Server.AddNewCommand(new RawCommand(command));

            Box.Clear();
        }
    }
}
