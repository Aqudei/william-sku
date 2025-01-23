using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using NLog;
using william_sku.Data;
using william_sku.Models;

namespace william_sku.ViewModels;

internal class SettingsViewModel : BindableBase, INavigationAware
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Database _database;
    private readonly IRegionManager _regionManager;

    private DelegateCommand? _goBackCommand;
    private string? _newHeaderDisplay = string.Empty;
    private bool _newHeaderIsRange;
    private bool _newHeaderIsRequired;
    private string? _newHeaderName;

    private DelegateCommand? _removeSelectedHeadersCommand;

    private DelegateCommand? _saveHeaderCommand;

    public SettingsViewModel(Database database, IRegionManager regionManager)
    {
        _database = database;
        _regionManager = regionManager;

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
        get { return _saveHeaderCommand ??= new DelegateCommand(OnSaveNewHeader); }
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
            var selected = Headers.Where(h => h is { IsSelected: true, Required: false });

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

    private async void OnSaveNewHeader()
    {
        try
        {
            var newHeader = new Header
            {
                Display = NewHeaderDisplay,
                Name = NewHeaderName,
                Range = NewHeaderIsRange,
                Required = NewHeaderIsRequired
            };

            _database.SaveNewHeader(newHeader);
            await FetchHeaders();
        }
        catch (Exception e)
        {
            Logger.Error(e);
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
        if (nameof(NewHeaderDisplay) == e.PropertyName) NewHeaderName = NewHeaderDisplay.Replace(" ", "");
    }
}