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
        private readonly IRegionManager _regionManager;

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
                        Items.DefaultView.RowFilter = $"{data.SelectedField} LIKE '%{data.SearchText}%'";
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
            //_dialogService.ShowDialog("Settings");

            _regionManager.RequestNavigate("MainRegion", "Settings");
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
                Task.Run(async () =>
                {
                    var progress = await _dialogCoordinator.ShowProgressAsync(this,
                        "Please wait", $"Deleting bulk based on MC# from {dialog.FileName}");
                    progress.SetIndeterminate();
                    try
                    {
                        var dataTable = Utils.WorksheetToDataTable(dialog.FileName, true, headers);

                        if (dataTable != null && dataTable.Rows.Count > 0)
                        {
                            foreach (DataRow row in dataTable.Rows)
                            {
                                var mcNum = row["MCNumber"];
                                _database.Delete(mcNum);
                            }

                            await LoadItems();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    finally
                    {
                        await progress.CloseAsync();
                    }
                });
            }

        }

        private void OnImportFile()
        {
            var dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Task.Run(async () =>
                {
                    var headers = _database.ListHeaders().ToArray();

                    var progress = await _dialogCoordinator.ShowProgressAsync(this, "Please wait", $"Importing {dialog.FileName}");
                    progress.SetIndeterminate();
                    try
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

                        await LoadItems();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    finally
                    {
                        await progress.CloseAsync();
                    }
                });
            }
        }

        private async Task LoadItems()
        {
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
        }

        public DataViewModel(Database database, IDialogService dialogService, IDialogCoordinator dialogCoordinator, IRegionManager regionManager)
        {
            _database = database;
            _dialogService = dialogService;
            _dialogCoordinator = dialogCoordinator;
            _regionManager = regionManager;
            Task.Run(LoadItems);
        }
    }
}
