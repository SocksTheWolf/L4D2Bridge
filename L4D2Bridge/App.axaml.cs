using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using L4D2Bridge.ViewModels;
using L4D2Bridge.Views;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TwitchLib.EventSub.Websockets.Extensions;
using L4D2Bridge.Models;
using Microsoft.Extensions.Logging;
using System;

namespace L4D2Bridge;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddHostedService<TwitchEventSubService>();
        // Push the twitch event service into the system as a required service. If it's missing, then it will be null.
        collection.AddSingleton<MainViewModel>(provider => {
            var hostedServ = provider.GetService<IHostedService>();
            if (hostedServ == null)
                return null;
            return new MainViewModel((TwitchEventSubService)hostedServ);
        });
        // I really don't care for these, but this is needed otherwise it cannot resolve logger types.
        collection.AddSingleton<ILogger>(s => s.GetRequiredService<ILogger<TwitchEventSubService>>());
        collection.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
        });
    }
}

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        var diCollection = new ServiceCollection();
        diCollection.AddTwitchLibEventSubWebsockets();
        diCollection.AddCommonServices();

        // Build and get the view model
        var services = diCollection.BuildServiceProvider();
        var vm = services.GetRequiredService<MainViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
