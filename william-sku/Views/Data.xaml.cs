using DryIoc.ImTools;
using System;
using System.Collections.Generic;
using System.Data.Common;
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

namespace william_sku.Views
{
    /// <summary>
    /// Interaction logic for Data.xaml
    /// </summary>
    public partial class Data : UserControl
    {
        public Data()
        {
            InitializeComponent();
        }

     

        private void SaveColumnOrdering(object sender, RoutedEventArgs e)
        {
            var columnsState = ItemsDataGrid.Columns.Select(i => i.Header.ToString());
            Properties.Settings.Default.ColumnsOrdering = JsonSerializer.Serialize(columnsState);
            Properties.Settings.Default.Save();
        }
    }
}
