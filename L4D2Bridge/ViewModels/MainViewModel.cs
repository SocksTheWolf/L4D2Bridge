using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
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
    private TwitchService? Twitch { get; set; }
    private Button? PauseButton { get; set; }

    [ObservableProperty]
    public string pauseButtonText = string.Empty;

    [ObservableProperty]
    public bool isPaused = false;

    [ObservableProperty]
    public string pauseTip = "Click here to pause the server";

    public MainViewModel()
    {
        // Load all configuration data
        LoadConfigs();

        // Start the console service
        Console.Start();

        // Set the default pause glyph
        SetPauseGlyph("f04b");

        /* RCON */
#pragma warning disable CS8604 // Possible null reference argument.
        Server = new RCONService(Config);
#pragma warning restore CS8604 // Possible null reference argument.
        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, Server);
        Server.OnPauseStatus = OnPauseStatusUpdate;
        Server.Start();

        /* Rules Engine */
        Rules = new RulesService();
        Rules.OnConsolePrint = (msg) => Console.AddMessage(msg, Rules);
        Rules.LoadActions(ref Config.Actions);
        Rules.Start();

        /* Tiltify */
        if (Config.TiltifySettings != null && Config.TiltifySettings.Enabled)
        {
            CharityTracker = new TiltifyService(Config.TiltifySettings);
            CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, CharityTracker);
            CharityTracker.OnSourceEvent = async (data) =>
            {
                Console.AddMessage($"{data.Name} donated {data.Amount}", CharityTracker);
                List<L4D2Action> Commands = await Rules.ExecuteAsync(CharityTracker.GetWorkflow(), data);
                Server.AddNewActions(Commands, data.Name);
            };
            CharityTracker.OnAuthUpdate = (data) =>
            {
                Config.TiltifySettings.OAuthToken = data.OAuthToken;
                if (!string.IsNullOrWhiteSpace(data.RefreshToken))
                    Config.TiltifySettings.RefreshToken = data.RefreshToken;
                Config.SaveConfigData();
                Console.AddMessage("OAuth Data Updated!", CharityTracker);
            };
            CharityTracker.Start();
        }

        /* Twitch */
        if (Config.TwitchSettings != null && Config.TwitchSettings.Enabled)
        {
            Twitch = new TwitchService(Config.TwitchSettings);
            Twitch.OnConsolePrint = (msg) => Console.AddMessage(msg, Twitch);
            Twitch.OnSourceEvent = async (data) => {
                List<L4D2Action> Commands = await Rules.ExecuteAsync(Twitch.GetWorkflow(), data);
                Server.AddNewActions(Commands, data.Name);
            };
            Twitch.Start();
        }

        Config.SaveConfigData();
        if (!Config.IsValid)
            Console.AddMessage("Invalid configuration, please check configs and restart", ConsoleSources.Main);
        else
            Console.AddMessage("Operations Running!", ConsoleSources.Main);
    }

    // Separated into a different function to allow for reloading of data
    private void LoadConfigs()
    {
        Config = ConfigData.LoadConfigData();

        // Push the command prefs to the command builder
        L4D2CommandBuilder.Initialize(Config);
    }

    // Flags our UI if the status of the server is paused
    public void OnPauseStatusUpdate(bool CurrentPauseStatus)
    {
        // This redirects the event to run on the UI thread.
        Dispatcher.UIThread.Post(() => PushPauseStatusUpdate(CurrentPauseStatus));
    }
    private void PushPauseStatusUpdate(bool CurrentPauseStatus)
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
            string loweredCommand = command.ToLower();
            if (loweredCommand == "clear")
                Console.ClearAllMessages();
            else if (loweredCommand == "reload")
            {
                Console.AddMessage("Attempting to reload configuration...", ConsoleSources.Main);
                // Load up our configs again
                LoadConfigs();
                // Restart the rules engine
                Rules.LoadActions(ref Config.Actions);
                Rules.Start();
                Console.AddMessage("Configuration Reloaded", ConsoleSources.Main);
            }
            else
                Server.AddNewCommand(new RawCommand(command));

            Box.Clear();
        }
    }
}
