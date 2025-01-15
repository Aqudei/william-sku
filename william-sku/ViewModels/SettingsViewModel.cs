using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using william_sku.Data;
using william_sku.Models;

namespace william_sku.ViewModels
{
    internal class SettingsViewModel : BindableBase, INavigationAware
    {
        private readonly Database _database;
        private readonly IRegionManager _regionManager;
        private string _newHeaderName;
        private string _newHeaderDisplay;
        private bool _newHeaderIsRange;
        private bool _newHeaderIsRequired;

        public ObservableCollection<Header> Headers { get; set; } = new();

        public string NewHeaderName { get => _newHeaderName; set => SetProperty(ref _newHeaderName, value); }
        public string NewHeaderDisplay { get => _newHeaderDisplay; set => SetProperty(ref _newHeaderDisplay, value); }
        public bool NewHeaderIsRange { get => _newHeaderIsRange; set => SetProperty(ref _newHeaderIsRange, value); }
        public bool NewHeaderIsRequired { get => _newHeaderIsRequired; set => SetProperty(ref _newHeaderIsRequired, value); }

        private DelegateCommand _removeSelectedHeadersCommand;

        public DelegateCommand RemoveSelectedHeadersCommand
        {
            get { return _removeSelectedHeadersCommand ??= new DelegateCommand(OnRemoveSelectedHeader); }
        }

        private async void OnRemoveSelectedHeader()
        {
            var selected = Headers.Where(h => h.IsSelected);

            foreach (var header in selected)
            {
                _database.DeleteHeader(header);
            }

            await FetchHeaders();
        }

        private DelegateCommand _saveHeaderCommand;

        public DelegateCommand SaveHeaderCommand
        {
            get { return _saveHeaderCommand ??= new DelegateCommand(OnSaveNewHeader); }
        }

        private DelegateCommand _goBackCommand;

        public DelegateCommand GoBackCommand
        {
            get { return _goBackCommand ??= new DelegateCommand(OnGoBack); }
        }

        private void OnGoBack()
        {
            var canGoBack = _regionManager.Regions["MainRegion"].NavigationService.Journal.CanGoBack;

            _regionManager.RequestNavigate("MainRegion", "Data");
        }

        private async void OnSaveNewHeader()
        {
            var newHeader = new Header
            {
                Display = NewHeaderDisplay,
                Name = NewHeaderName,
                Range = NewHeaderIsRange,
                Required = NewHeaderIsRequired,
            };

            _database.SaveNewHeader(newHeader);
            await FetchHeaders();
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

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Task.Run(FetchHeaders);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        { }

        public SettingsViewModel(Database database, IRegionManager regionManager)
        {
            _database = database;
            _regionManager = regionManager;

            PropertyChanged += SettingsViewModel_PropertyChanged;
        }

        private void SettingsViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (nameof(NewHeaderDisplay) == e.PropertyName)
            {
                NewHeaderName = NewHeaderDisplay.Replace(" ", "");
            }
        }
    }
}
