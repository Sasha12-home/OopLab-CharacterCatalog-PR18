using System.Windows;
using OopLab.Core.ViewModels;

namespace OopLab.Gui;

public partial class CharacterDetailWindow : Window
{
    public CharacterDetailWindow(CharacterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Title = $"Картка персонажа — {viewModel.DisplayName}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
