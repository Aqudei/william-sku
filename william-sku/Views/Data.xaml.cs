using DryIoc.ImTools;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using william_sku.Data;

namespace william_sku.Views
{
    /// <summary>
    /// Interaction logic for Data.xaml
    /// </summary>
    public partial class Data : UserControl
    {
        private readonly Database _database;
        private readonly IDialogCoordinator _dialogCoordinator;

        public Data(Database database, IDialogCoordinator dialogCoordinator)
        {
            _database = database;
            _dialogCoordinator = dialogCoordinator;
            InitializeComponent();
        }

        private async void SaveColumnOrdering(object sender, RoutedEventArgs e)
        {
            var columnsState = ItemsDataGrid.Columns.ToDictionary(c => c.DisplayIndex);

            var orderedHeaders = columnsState.OrderBy(i => i.Key).Select(h => h.Value.Header.ToString()).ToList();
            _database.SaveColumnOrdering(orderedHeaders);

            await _dialogCoordinator.ShowMessageAsync(DataContext, "Reorder Columns",
                $"New Column ordering: \n\n{string.Join(',', orderedHeaders)}");
        }
    }
}
