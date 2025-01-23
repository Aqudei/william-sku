using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using NLog;
using william_sku.Data;

namespace william_sku.ViewModels;

internal class DataViewModel : BindableBase
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly Database _database;
    private readonly IDialogCoordinator _dialogCoordinator;
    private readonly IDialogService _dialogService;
    private readonly IRegionManager _regionManager;

    private DelegateCommand? _bulkDeleteCommand;

    private DelegateCommand? _exportCommand;
    private DelegateCommand? _importCommand;


    private DataTable _items = new();
    private DelegateCommand? _searchCommand;
    private DelegateCommand? _settingsCommand;
    private DelegateCommand? _updateOnlyCommand;

    public DataViewModel(Database database, IDialogService dialogService, IDialogCoordinator dialogCoordinator,
        IRegionManager regionManager)
    {
        _database = database;
        _dialogService = dialogService;
        _dialogCoordinator = dialogCoordinator;
        _regionManager = regionManager;
        Task.Run(LoadItems);
    }

    public DataTable Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public DelegateCommand SearchCommand => _searchCommand ??= new DelegateCommand(OnSearch);

    public DelegateCommand ExportCommand
    {
        get { return _exportCommand ??= new DelegateCommand(OnExport); }
    }

    public DelegateCommand ImportCommand
    {
        get { return _importCommand ??= new DelegateCommand(OnImportFile); }
    }

    public DelegateCommand SettingsCommand => _settingsCommand ??= new DelegateCommand(OnSettings);

    public DelegateCommand BulkDeleteCommand
    {
        get { return _bulkDeleteCommand ??= new DelegateCommand(OnBulkDelete); }
    }

    private void OnSearch()
    {
        _dialogService.ShowDialog("Search", async void (dialogResult) =>
        {
            try
            {
                if (dialogResult.Result != ButtonResult.OK)
                    return;

                if (dialogResult.Parameters["Data"] is not SearchViewModel data ||
                    (string.IsNullOrEmpty(data.SelectedField) && string.IsNullOrEmpty(data.SelectedRangeField)))
                {
                    await LoadItemsWithProgressBar();
                    return;
                }

                var items = _database.ListItemsAsDataTable();
                var result1 = new List<DataRow>();
                var result2 = new List<DataRow>();

                // Filter by SelectedField if applicable
                if (!string.IsNullOrEmpty(data.SelectedField))
                    result1 = items.AsEnumerable()
                        .Where(row => row.Field<string>(data.SelectedField)?.Contains(data.SearchText) == true)
                        .ToList();

                // Filter by SelectedRangeField if applicable
                if (!string.IsNullOrEmpty(data.SelectedRangeField))
                {
                    if (data.SelectedRangeField is "AddedDate" or "LastUpdate")
                    {
                        result2 = _database
                            .ListItemsBetweenDatesAsDataTable(data.SelectedRangeField, data.SearchFrom, data.SearchTo)
                            .AsEnumerable()
                            .ToList();
                    }
                    else
                    {
                        var rgx = new Regex(@"\d+$");
                        if (int.TryParse(rgx.Match(data.SearchFrom).Value, out var searchFrom) &&
                            int.TryParse(rgx.Match(data.SearchTo).Value, out var searchTo))
                            result2 = items.AsEnumerable()
                                .Where(row =>
                                {
                                    var value = rgx.Match(row.Field<string>(data.SelectedRangeField) ?? string.Empty)
                                        .Value;
                                    return int.TryParse(value, out var number) && number >= searchFrom &&
                                           number <= searchTo;
                                })
                                .ToList();
                    }
                }

                // Combine results based on conditions
                IEnumerable<DataRow> combinedResults;
                var comparer = new MCRecordsComparer();
                if (!string.IsNullOrEmpty(data.SelectedField) &&
                    !string.IsNullOrEmpty(data.SelectedRangeField))
                    combinedResults = result1.Intersect(result2, comparer);
                else
                    combinedResults = result1.Union(result2, comparer);

                // Update Items with combined results
                var dataRows
                    = combinedResults as DataRow[] ?? combinedResults.ToArray();
                if (dataRows.Any())
                    Items = dataRows.CopyToDataTable();
                else
                    Items.DefaultView.RowFilter = "1=0";
            }
            catch (Exception e)
            {
                Logger.Error(e);
                await _dialogCoordinator.ShowMessageAsync(this, "Search Error", e.Message);
            }
        });
    }

    private async Task LoadItemsWithProgressBar()
    {
        var progress = await _dialogCoordinator.ShowProgressAsync(this, "Please wait", "Loading items");
        progress.SetIndeterminate();

        try
        {
            await Task.Run(LoadItems);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
        finally
        {
            await progress.CloseAsync();
        }
    }

    public DelegateCommand UpdateOnlyCommand
    {
        get { return _updateOnlyCommand ??= new DelegateCommand(OnUpdateOnly); }
    }

    private void OnUpdateOnly()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel / CSV Files (*.xlsx;*.csv)|*.xlsx;*.csv"
        };

        var result = dialog.ShowDialog();
        if (result.HasValue && result.Value)
            Task.Run(async () =>
            {
                var headers = _database.ListHeaders().ToArray();

                var progress =
                    await _dialogCoordinator.ShowProgressAsync(this, "Please wait", $"Updating from: {dialog.FileName}");
                try
                {
                    var dataTable = Utils.WorksheetToDataTable(dialog.FileName, headers);
                    if (dataTable is { Rows.Count: > 0 })
                        for (var index = 0; index < dataTable.Rows.Count; index++)
                        {
                            var percentage = (double)index / dataTable.Rows.Count;
                            progress.SetProgress(percentage);

                            var row = dataTable.Rows[index];
                            var mcNum = row.Field<string>("MCNumber");
                            _database.UpdateOnly(mcNum, row);
                        }

                    await LoadItems();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    await _dialogCoordinator.ShowMessageAsync(this, "Import Error", ex.Message);
                }
                finally
                {
                    await progress.CloseAsync();
                }
            });
    }

    private async void OnExport()
    {
        try
        {
            var headers = _database.ListHeaders().ToDictionary(h => h.Name);

            var dialog = new SaveFileDialog
            {
                DefaultExt = "xlsx",
                Filter = "Excel Files (*.xlsx)|*.xlsx" // File type filter
            };
            var result = dialog.ShowDialog();
            if (!result.HasValue || !result.Value)
                return;

            var progress =
                await _dialogCoordinator.ShowProgressAsync(this, "Please wait", $"Exporting to {dialog.FileName}...");
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
        catch (Exception e)
        {
            await _dialogCoordinator.ShowMessageAsync(this, "Export Error", e.Message);
        }
    }

    private void OnSettings()
    {
        //_dialogService.ShowDialog("Settings");

        _regionManager.RequestNavigate("MainRegion", "Settings");
    }

    private void OnBulkDelete()
    {
        var headers = _database.ListHeaders().ToArray();
        var dialog = new OpenFileDialog();
        var result = dialog.ShowDialog();


        if (result.HasValue && result.Value)
            Task.Run(async () =>
            {
                var prompt = await _dialogCoordinator.ShowMessageAsync(this, "Confirm Action",
                    "Are you sure you want to continue with bulk delete operation?",
                    MessageDialogStyle.AffirmativeAndNegative);

                if (prompt == MessageDialogResult.Negative)
                    return;


                var progress = await _dialogCoordinator.ShowProgressAsync(this,
                    "Please wait", $"Deleting bulk based on MC# from {dialog.FileName}");
                progress.SetIndeterminate();
                try
                {
                    var dataTable = Utils.WorksheetToDataTable(dialog.FileName, headers);

                    if (dataTable.Rows.Count > 0)
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
                    await _dialogCoordinator.ShowMessageAsync(this, "Bulk Delete Error", ex.Message);
                }
                finally
                {
                    await progress.CloseAsync();
                }
            });
    }

    private void OnImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel / CSV Files (*.xlsx;*.csv)|*.xlsx;*.csv"
        };

        var result = dialog.ShowDialog();
        if (result.HasValue && result.Value)
            Task.Run(async () =>
            {
                var headers = _database.ListHeaders().ToArray();

                var progress =
                    await _dialogCoordinator.ShowProgressAsync(this, "Please wait", $"Importing {dialog.FileName}");
                try
                {
                    var dataTable = Utils.WorksheetToDataTable(dialog.FileName, headers);
                    if (dataTable is { Rows.Count: > 0 })
                        for (var index = 0; index < dataTable.Rows.Count; index++)
                        {
                            var percentage = (double)index / dataTable.Rows.Count;
                            progress.SetProgress(percentage);

                            var row = dataTable.Rows[index];
                            var mcNum = row.Field<string>("MCNumber");
                            _database.UpdateOrCreate(mcNum, row);
                        }

                    await LoadItems();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    await _dialogCoordinator.ShowMessageAsync(this, "Import Error", ex.Message);
                }
                finally
                {
                    await progress.CloseAsync();
                }
            });
    }

    private async Task LoadItems()
    {
        try
        {
            Items.Clear();
            var dt = _database.ListItemsAsDataTable();
            Debug.WriteLine($"Loaded a total of '{dt.Rows.Count}' items");
            await Application.Current.Dispatcher.InvokeAsync(() => { Items = dt; });
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

    public class MCRecordsComparer : IEqualityComparer<DataRow>
    {
        public bool Equals(DataRow? x, DataRow? y)
        {
            // Check for nulls
            if (x == null || y == null) return false;

            // Check if "MCNumber" exists and compare their values
            return x["MCNumber"]?.Equals(y["MCNumber"]) == true;
        }

        public int GetHashCode([DisallowNull] DataRow obj)
        {
            // Ensure "MCNumber" exists and return its hash code
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var value = obj["MCNumber"];

            return value.GetHashCode();
        }
    }
}