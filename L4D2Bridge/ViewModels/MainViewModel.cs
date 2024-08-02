﻿using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using L4D2Bridge.Models;
using System;

namespace L4D2Bridge.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ConfigData Config { get; private set; }
    public ConsoleService Console { get; set; } = new ConsoleService();
    private RCONService Server { get; set; }
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
        SetPauseGlyph("f04b");
        Config = ConfigData.LoadConfigData();
        Console.Initialize(Config);
        Server = new RCONService(Config);

        Server.OnConsolePrint = (msg) => Console.AddMessage(msg, EConsoleSource.RCON);
        Server.OnPauseStatus = OnPauseStatusUpdate;
        Server.Start();

        if (Config.TiltifySettings != null && Config.TiltifySettings.Enabled)
        {
            CharityTracker = new TiltifyService(Config.TiltifySettings);
            CharityTracker.OnConsolePrint = (msg) => Console.AddMessage(msg, EConsoleSource.Tiltify);
            CharityTracker.OnDonationReceived = (data) =>
            {
                Console.AddMessage($"{data.Name} donated {data.Amount} {data.Currency}", EConsoleSource.Tiltify);
            };
            CharityTracker.OnAuthUpdate = (data) =>
            {
                Config.TiltifySettings.OAuthToken = data.OAuthToken;
                Config.TiltifySettings.RefreshToken = data.RefreshToken;
                Config.SaveConfigData();
                Console.AddMessage("OAuth Data Updated!", EConsoleSource.Tiltify);
            };
            CharityTracker.Start();
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
