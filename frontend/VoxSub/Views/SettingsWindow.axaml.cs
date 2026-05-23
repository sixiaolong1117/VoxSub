using Avalonia.Controls;
using VoxSub.ViewModels;

namespace VoxSub.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel();
        viewModel.RequestClose += result => Close(result);
        DataContext = viewModel;
    }
}
