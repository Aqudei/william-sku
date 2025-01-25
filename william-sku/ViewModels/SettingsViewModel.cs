using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Xml.Linq;
using MahApps.Metro.Controls.Dialogs;
using NLog;
using william_sku.Data;
using william_sku.Models;

namespace william_sku.ViewModels;

internal class SettingsViewModel : BindableBase, INavigationAware
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Database _database;
    private readonly IRegionManager _regionManager;
    private readonly IDialogCoordinator _dialogCoordinator;

    private DelegateCommand? _goBackCommand;
    private string? _newHeaderDisplay = string.Empty;
    private bool _newHeaderIsRange;
    private bool _newHeaderIsRequired;
    private string? _newHeaderName;

    public Header? SelectedHeader
    {
        get => _selectedHeader;
        set => SetProperty(ref _selectedHeader, value);
    }

    private DelegateCommand? _removeSelectedHeadersCommand;

    private DelegateCommand? _saveHeaderCommand;
    private Header? _selectedHeader;
    private int _newHeaderId;

    public SettingsViewModel(Database database, IRegionManager regionManager, IDialogCoordinator dialogCoordinator)
    {
        _database = database;
        _regionManager = regionManager;
        _dialogCoordinator = dialogCoordinator;

        PropertyChanged += SettingsViewModel_PropertyChanged;
    }

    public ObservableCollection<Header> Headers { get; set; } = new();

    public string? NewHeaderName
    {
        get => _newHeaderName;
        set => SetProperty(ref _newHeaderName, value);
    }

    public string? NewHeaderDisplay
    {
        get => _newHeaderDisplay;
        set => SetProperty(ref _newHeaderDisplay, value);
    }

    public bool NewHeaderIsRange
    {
        get => _newHeaderIsRange;
        set => SetProperty(ref _newHeaderIsRange, value);
    }

    public bool NewHeaderIsRequired
    {
        get => _newHeaderIsRequired;
        set => SetProperty(ref _newHeaderIsRequired, value);
    }

    public DelegateCommand RemoveSelectedHeadersCommand
    {
        get { return _removeSelectedHeadersCommand ??= new DelegateCommand(OnRemoveSelectedHeader); }
    }

    public DelegateCommand SaveHeaderCommand
    {
        get { return _saveHeaderCommand ??= new DelegateCommand(OnSaveHeader); }
    }

    public DelegateCommand GoBackCommand
    {
        get { return _goBackCommand ??= new DelegateCommand(OnGoBack); }
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        Task.Run(FetchHeaders);
    }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async void OnRemoveSelectedHeader()
    {
        try
        {
            var selected = Headers.Where(h => h is { IsSelected: true, Required: false }).ToArray();
            var prompt = await _dialogCoordinator.ShowMessageAsync(this, "Confirm Delete",
                $"Are you sure you want to delete the following columns?\n\n{string.Join(',', selected.Select(s => s.Display))}", MessageDialogStyle.AffirmativeAndNegative);
            if (prompt == MessageDialogResult.Negative)
                return;

            foreach (var header in selected) _database.DeleteHeader(header);

            await FetchHeaders();
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    private void OnGoBack()
    {
        _regionManager.RequestNavigate("MainRegion", "Data");
    }
    private DelegateCommand _newHeaderCommand;

    public DelegateCommand NewHeaderCommand
    {
        get { return _newHeaderCommand ??= new DelegateCommand(OnNewHeader); }
    }

    private void OnNewHeader()
    {
        NewHeaderDisplay = NewHeaderName = "";
        NewHeaderIsRange = false;
        NewHeaderIsRequired = false;
        NewHeaderId = 0;
    }

    private async void OnSaveHeader()
    {
        try
        {
            var headerInfo = new Header
            {
                Display = NewHeaderDisplay,
                Name = NewHeaderName,
                Range = NewHeaderIsRange,
                Required = NewHeaderIsRequired,
                Id = NewHeaderId
            };

            _database.SaveHeader(headerInfo);
            await FetchHeaders();

            var message = await _dialogCoordinator.ShowMessageAsync(this, "Success", "Header info saved!");

        }
        catch (Exception e)
        {
            Logger.Error(e);
            var message = await _dialogCoordinator.ShowMessageAsync(this, "Error", $"Something went wrong.\n\n{e.Message}");
        }
    }

    private async Task FetchHeaders()
    {
        var headers = _database.ListHeaders().ToArray();

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Headers.Clear();
            Headers.AddRange(headers);
        });
    }

    private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NewHeaderDisplay):
                NewHeaderName = NewHeaderDisplay.Replace(" ", "").Replace("#", "Number");
                break;
            case nameof(SelectedHeader):
                {
                    if (SelectedHeader != null)
                    {
                        NewHeaderDisplay = SelectedHeader.Display;
                        NewHeaderName = SelectedHeader.Name;
                        NewHeaderIsRange = SelectedHeader.Range;
                        NewHeaderIsRequired = SelectedHeader.Required;
                        NewHeaderId = SelectedHeader.Id;
                    }
                }
                break;
        }
    }

    public int NewHeaderId
    {
        get => _newHeaderId;
        set => SetProperty(ref _newHeaderId, value);
    }
}