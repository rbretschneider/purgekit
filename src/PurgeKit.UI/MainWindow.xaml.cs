using System.Windows;
using PurgeKit.UI.ViewModels;

namespace PurgeKit.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
