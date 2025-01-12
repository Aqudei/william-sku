using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using william_sku.Data;

namespace william_sku.ViewModels
{
    internal class DataViewModel : BindableBase
    {

        private DelegateCommand _importCommand;
        private readonly Database _database;
        public DataTable Items { get => _items; set => SetProperty(ref _items, value); }


        public DelegateCommand ImportCommand
        {
            get { return _importCommand ??= new DelegateCommand(OnImportFile); }
        }

        private DelegateCommand _bulkDeleteCommand;
        private DataTable _items = new DataTable();

        public DelegateCommand BulkDeleteCommand
        {
            get { return _bulkDeleteCommand ??= new DelegateCommand(OnBulkDelete); }
        }

        private void OnBulkDelete()
        {
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var dataTable = Utils.WorksheetToDataTable(dialog.FileName, true);
                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var mcNum = row["MCNumber"];
                        _database.Delete(mcNum);
                    }

                    Task.Run(LoadItems);

                }
            }
        }

        private void OnImportFile()
        {
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var dataTable = Utils.WorksheetToDataTable(dialog.FileName, true);
                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var mcNum = row["MCNumber"];
                        _database.UpdateOrCreate(mcNum, row);
                    }
                }

                Task.Run(LoadItems);
            }
        }

        public DataViewModel(Database database)
        {
            _database = database;

            Task.Run(LoadItems);
        }

        private async Task LoadItems()
        {
            Items.Clear();
            var dt = _database.ListItems();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Items = dt;
            });
        }
    }
}
