using MahApps.Metro.Controls.Dialogs;
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
        private DataTable _items = new DataTable();
        private DelegateCommand _importCommand;
        private readonly Database _database;
        private readonly IDialogService _dialogService;
        private readonly IDialogCoordinator _dialogCoordinator;

        public DataTable Items { get => _items; set => SetProperty(ref _items, value); }


        public DelegateCommand SearchCommand => _searchCommand ??= new DelegateCommand(OnSearch);

        private void OnSearch()
        {
            _dialogService.ShowDialog("Search", result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var data = result.Parameters["Data"] as SearchViewModel;
                    if (data != null)
                    {
                        if (data.SelectedField == SearchViewModel.NoneField)
                        {
                            Items.DefaultView.RowFilter = $"1=1";
                            return;
                        }
                        Items.DefaultView.RowFilter = $"{data.SelectedField} = '{data.SearchText}'";
                    }
                }
            });
        }

        public DelegateCommand ImportCommand
        {
            get { return _importCommand ??= new DelegateCommand(OnImportFile); }
        }


        public DelegateCommand SettingsCommand { get => _settingsCommand ??= new DelegateCommand(OnSettings); }

        private void OnSettings()
        {
            _dialogService.ShowDialog("Settings");
        }

        private DelegateCommand _bulkDeleteCommand;
        private DelegateCommand _settingsCommand;
        private DelegateCommand _searchCommand;

        public DelegateCommand BulkDeleteCommand
        {
            get { return _bulkDeleteCommand ??= new DelegateCommand(OnBulkDelete); }
        }

        private void OnBulkDelete()
        {
            var headers = _database.ListHeaders().ToArray();
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var dataTable = Utils.WorksheetToDataTable(dialog.FileName, true, headers);

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
            var headers = _database.ListHeaders().ToArray();
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var dataTable = Utils.WorksheetToDataTable(dialog.FileName, true, headers);
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



        private async void LoadItems()
        {
            var progess = await _dialogCoordinator.ShowProgressAsync(this, "Please wait.", "Loading items...");
            progess.SetIndeterminate();

            try
            {
                Items.Clear();
                var dt = _database.ListItems();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Items = dt;
                });


            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                await progess.CloseAsync();
            }
        }

        public DataViewModel(Database database, IDialogService dialogService, IDialogCoordinator dialogCoordinator)
        {
            _database = database;
            _dialogService = dialogService;
            _dialogCoordinator = dialogCoordinator;
            Task.Run(LoadItems);
        }
    }
}
