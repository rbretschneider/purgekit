using System.Windows;
using System.Windows.Controls;
using PurgeKit.UI.ViewModels;

namespace PurgeKit.UI.Views;

public partial class ProgramsView : UserControl
{
    public ProgramsView()
    {
        InitializeComponent();
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header
            && header.Tag is string column
            && DataContext is ProgramsViewModel vm)
        {
            vm.SortByColumnCommand.Execute(column);
        }
    }
}
