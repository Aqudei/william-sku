using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using william_sku.Data;

namespace william_sku.ViewModels
{
    internal class DataViewModel
    {

        private DelegateCommand _importCommand;
        private readonly Database _database;
        public DataTable Items { get; set; } = new DataTable();



        public DelegateCommand ImportCommand
        {
            get { return _importCommand ??= new DelegateCommand(OnImportFile); }
        }

        private DelegateCommand _bulkDeleteCommand;

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

        private void LoadItems()
        {
            Items.Rows.Clear();
            Items.Columns.Clear();

            var dt = _database.ListItems();
            foreach (DataColumn col in dt.Columns)
            {
                Items.Columns.Add(col.ColumnName, col.DataType);
            }

            foreach (DataRow row in dt.Rows)
            {
                Items.ImportRow(row);
            }
        }
    }
}
