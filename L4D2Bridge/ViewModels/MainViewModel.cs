using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private RCONService? Server { get; set; }
    private RulesService Rules { get; set; } = new RulesService();
    private TiltifyService? CharityTracker { get; set; }
    private TwitchService? Twitch { get; set; }
    private TestService? Test { get; set; }

    // GUI Objects
    public static Button? PauseButton { get; set; } = null;
    public static TextBox? ServerInput { get; set; } = null;

    // Textbox History
    private List<string> HistoryItems = new List<string>();
    private int HistoryIndex = 0;

    [ObservableProperty]
    public string pauseButtonText = string.Empty;

    [ObservableProperty]
    public bool isPaused = false;

    [ObservableProperty]
    public string pauseTip = "Click here to pause the server";

    public MainViewModel()
    {
        // Add an override to the server input box so we can properly handle arrow keys history navigation.
        ServerInput?.AddHandler(InputElement.KeyDownEvent, OnTextBoxKey_Down, RoutingStrategies.Tunnel);

        /* Rules Engine */
        Rules.OnConsolePrint = (msg) => Console.AddMessage(msg, Rules);

        // Load all configuration data
        LoadConfigs();

        // Preallocate the maximum size of the history items list
#pragma warning disable CS8602 // Possible null reference argument.
        HistoryItems.Capacity = Config.MaxInputHistory;
#pragma warning restore CS8602 // Possible null reference argument.

        // Start the console service
        Console.Start(Config.MaxMessageLifetime);


        // Set the default pause glyph
        SetPauseGlyph("f04b");

        /* RCON */
        if (Config.IsValid)
        {
            Server = new RCONService(Config.ServerSettings);
            Server.OnConsolePrint = (msg) => Console.AddMessage(msg, Server);
            Server.OnPauseStatus = OnPauseStatusUpdate;
            Server.Start();
        }

        /* Tiltify */
        if (Config.IsUsingTiltify())
        {
            CharityTracker = new TiltifyService(Config.TiltifySettings);
            CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, CharityTracker);
            CharityTracker.OnSourceEvent += async (data) => {
                Console.AddMessage($"{data.Name} donated {data.Amount}", CharityTracker);
                List<L4D2Action> Commands = await Rules.ExecuteAsync(CharityTracker.GetWorkflow(), data);
                Server?.AddNewActions(Commands, data.Name);

                PostActions(ref Commands, CharityTracker.GetSource());
            };
            CharityTracker.OnAuthUpdate = (data) => {
                Config.TiltifySettings.OAuthToken = data.OAuthToken;
                if (!string.IsNullOrWhiteSpace(data.RefreshToken))
                    Config.TiltifySettings.RefreshToken = data.RefreshToken;
                Config.SaveConfigData();
                Console.AddMessage("OAuth Data Updated!", CharityTracker);
            };
            CharityTracker.Start();
        }

        /* Twitch */
        if (Config.IsUsingTwitch())
        {
            Twitch = new TwitchService(Config.TwitchSettings);
            Twitch.OnConsolePrint = (msg) => Console.AddMessage(msg, Twitch);
            Twitch.OnSourceEvent += async (data) => {
                List<L4D2Action> Commands = await Rules.ExecuteAsync(Twitch.GetWorkflow(), data);
                Server?.AddNewActions(Commands, data.Name);
                PostActions(ref Commands, Twitch.GetSource());
            };
            Twitch.Start();

            if (CharityTracker != null && Config.TwitchSettings.PostMessageOnTiltifyDonations)
            {
                // Send a message to every twitch channel we are currently connected to
                CharityTracker.OnSourceEvent += (data) => {
                    Twitch.SendMessageToAllChannels($"{data.Name} just donated ${data.Amount} with message '{data.Message}'");
                };
            }
        }

        /* Test Service */
        if (Config.IsUsingTest())
        {
            Test = new TestService(Config.TestSettings);
            Test.OnConsolePrint = (msg) => Console.AddMessage(msg, Test);
            Test.OnSourceEvent += async (data) => {
                List<L4D2Action> Commands = await Rules.ExecuteAsync(Test.GetWorkflow(), data);
                Server?.AddNewActions(Commands, data.Name);
                PostActions(ref Commands, Test.GetSource());
            };
            Test.Start();
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

        // Push the rules engine data
        Rules.LoadActions(ref Config.Actions);
        Rules.Start();
    }

    private void PostActions(ref readonly List<L4D2Action> Actions, ConsoleSources Source)
    {
        // Print the actions taken to the console
        string ActionTaken = RulesService.ResultActionsToString(in Actions);
        Console.AddMessage(ActionTaken, Source);

        // Dump them also to twitch chat
        if (Config.IsUsingTwitch() && Config.TwitchSettings.PostEventActionsToChat)
            Twitch?.SendMessageToAllChannels(ActionTaken);
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
        Server?.AddNewCommand(new TogglePauseCommand());
    }

    public void OnTextBoxKey_Down(object? source, KeyEventArgs args)
    {
        bool isUp = (args.Key == Key.Up);
        if (args.Key == Key.Down || isUp)
        {
            args.Handled = true;
            HistoryIndex += (isUp) ? 1 : -1;
            if (HistoryIndex < 0)
                HistoryIndex = HistoryItems.Count - 1;
            else if (HistoryIndex >= HistoryItems.Count)
                HistoryIndex = 0;

            string historyValue = HistoryItems[HistoryIndex];
            if (ServerInput != null)
                ServerInput.Text = historyValue;
        }
    }

    public void OnServerCommand_Sent(object msg)
    {
        TextBox Box = ((TextBox)msg);
        string? command = Box.Text;
        Box.Clear();

        if (!string.IsNullOrEmpty(command))
        {
            string loweredCommand = command.ToLower();
            if (loweredCommand == "clear" || loweredCommand == "cls")
                Console.ClearAllMessages();
            else if (loweredCommand == "reload")
            {
                Console.AddMessage("Attempting to reload configuration...", ConsoleSources.Main);
                // Load up our configs again
                LoadConfigs();
                Console.AddMessage("Configuration Reloaded", ConsoleSources.Main);
            }
            else if (loweredCommand == "pause" || loweredCommand == "unpause" || loweredCommand == "resume")
            {
                Test?.TogglePause();
            }
            else
                Server?.AddNewCommand(new RawCommand(command));

            // Invalidate our index, so that we will move appropriately next time we press arrow keys
            HistoryIndex = -1;

            // Push any of the commands we get to the history item list
            HistoryItems.Insert(0, command);
            if (HistoryItems.Count > Config.MaxInputHistory)
            {
                HistoryItems.RemoveAt(HistoryItems.Count - 1);
            } 
        }
    }
}
