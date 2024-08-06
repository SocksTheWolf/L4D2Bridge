using Avalonia.Controls;
using L4D2Bridge.Models;
using L4D2Bridge.ViewModels;

namespace L4D2Bridge.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // Technically, this runs on the UI thread anyways, so we don't break MVVM :)
        // This thing is annoying and I hate it.
        ConsoleService.Griddy = ConsoleLog;
        MainViewModel.PauseButton = PauseStatus;
    }
}
