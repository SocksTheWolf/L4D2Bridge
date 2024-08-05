﻿using Avalonia.Controls;
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
    private RulesService Rules { get; set; } = new RulesService();
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
        /* Rules Engine */
        Rules.OnConsolePrint = (msg) => Console.AddMessage(msg, Rules);

        // Load all configuration data
        LoadConfigs();

        // Start the console service
#pragma warning disable CS8602 // Possible null reference argument.
        Console.Start(Config.MaxMessageLifetime);
#pragma warning restore CS8602 // Possible null reference argument.

        // Set the default pause glyph
        SetPauseGlyph("f04b");

        /* RCON */
        Server = new RCONService(Config);
        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, Server);
        Server.OnPauseStatus = OnPauseStatusUpdate;
        Server.Start();

        /* Tiltify */
        if (Config.IsUsingTiltify())
        {
            CharityTracker = new TiltifyService(Config.TiltifySettings);
            CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, CharityTracker);
            CharityTracker.OnSourceEvent += async (data) => {
                Console.AddMessage($"{data.Name} donated {data.Amount}", CharityTracker);
                List<L4D2Action> Commands = await Rules.ExecuteAsync(CharityTracker.GetWorkflow(), data);
                Server.AddNewActions(Commands, data.Name);

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
                Server.AddNewActions(Commands, data.Name);
                PostActions(ref Commands, Twitch.GetSource());
            };
            Twitch.Start();

            if (CharityTracker != null && Config.TwitchSettings.MessageOnTiltifyDonations)
            {
                // Send a message to every twitch channel we are currently connected to
                CharityTracker.OnSourceEvent += (data) => {
                    Twitch.SendMessageToAllChannels($"{data.Name} just donated ${data.Amount} with message '{data.Message}'");
                };
            }
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
        if (Config.IsUsingTwitch() && Config.TwitchSettings.SendActionsToChat)
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
        PauseButton ??= ((Button)msg);
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
                Console.AddMessage("Configuration Reloaded", ConsoleSources.Main);
            }
            else
                Server.AddNewCommand(new RawCommand(command));

            Box.Clear();
        }
    }
}
