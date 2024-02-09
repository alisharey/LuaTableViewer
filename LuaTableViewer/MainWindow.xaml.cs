using System.Windows;
using System.Windows.Input;

using MaterialDesignThemes.Wpf;

namespace LuaTableViewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, OnClose));
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        
    }
}
