using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NLog;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using william_sku.Data;

namespace william_sku.ViewModels
{
    internal class DataViewModel : BindableBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
            _dialogService.ShowDialog("Search", dialogResult =>
            {
                if (dialogResult.Result == ButtonResult.OK)
                {
                    var data = dialogResult.Parameters["Data"] as SearchViewModel;
                    if (data != null)
                    {
                        if (data.SelectedField == SearchViewModel.NoneField && data.SelectedRangeField == SearchViewModel.NoneField)
                        {
                            return;
                        }

                        var items = _database.ListItemsAsDataTable();
                        var result1 = new List<DataRow>();
                        var result2 = new List<DataRow>();

                        if (data.SelectedField != SearchViewModel.NoneField)
                        {
                            var searchResult = from row in items.AsEnumerable()
                                               where row.Field<string>(data.SelectedField).Contains(data.SearchText)
                                               select row;

                            if (searchResult != null && searchResult.Any())
                            {
                                result1.AddRange(searchResult);
                            }
                        }

                        if (data.SelectedRangeField != SearchViewModel.NoneField)
                        {
                            var rgx = new Regex(@"\d+$");
                            var query1 = from row in items.AsEnumerable()
                                         select new { Regex = rgx.Match(row.Field<string>(data.SelectedRangeField)).Value, Row = row };

                            var query2 = from item in query1
                                         let regexValue = item.Regex
                                         let number = int.TryParse(regexValue, out var result) ? result : (int?)null
                                         where number.HasValue && number.Value >= int.Parse(data.SearchFrom) && number.Value <= int.Parse(data.SearchTo)
                                         select item.Row;
                            if (query2 != null && query2.Any())
                            {
                                result2.AddRange(query2);
                            }
                        }

                        var result = result1.Intersect(result2);
                        if (result.Any())
                        {
                            Items = result.CopyToDataTable();
                        }
                        else
                        {
                            Items.Rows.Clear();
                        }

                        // Items.DefaultView.RowFilter = $"{data.SelectedField} LIKE '%{data.SearchText}%'";
                    }
                }
            });
        }

        private DelegateCommand _exportCommand;

        public DelegateCommand ExportCommand
        {
            get { return _exportCommand ??= new DelegateCommand(OnExport); }
        }

        private async void OnExport()
        {
            var headers = _database.ListHeaders().ToDictionary(h => h.Name);

            var dialog = new SaveFileDialog
            {
                DefaultExt = "xlsx",
                Filter = "Excel Files (*.xlsx)|*.xlsx", // File type filter
            };
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                var progress = await _dialogCoordinator.ShowProgressAsync(this, "Please wait", $"Exporting to {dialog.FileName}...");
                progress.SetIndeterminate();

                try
                {
                    Utils.ExportToExcel(Items, dialog.FileName, headers);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to Export to Excel.");
                    Logger.Error(ex);
                }
                finally
                {
                    await progress.CloseAsync();
                }
            }
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
                        var dataTable = Utils.WorksheetToDataTable(dialog.FileName, headers);

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
                        Logger.Error(ex);
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

                        var dataTable = Utils.WorksheetToDataTable(dialog.FileName, headers);
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
                        Logger.Error(ex);
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
                var dt = _database.ListItemsAsDataTable();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Items = dt;
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
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
