using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using william_sku.Data;

namespace william_sku.ViewModels
{
    internal class SearchViewModel : BindableBase, IDialogAware
    {
        public const string NoneField = "-None-";
        private readonly Database _database;
        private DelegateCommand _closeCommand;

        public DialogCloseListener RequestClose { get; }
        public ObservableCollection<string> Fields { get; set; } = new();
        public ObservableCollection<string> RangeFields { get; set; } = new();
        public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
        public string SelectedField { get; set; }

        public string SelectedRangeField { get; set; }
        public string SearchFrom { get => _searchFrom; set => SetProperty(ref _searchFrom, value); }
        public string SearchTo { get => _searchTo; set => SetProperty(ref _searchTo, value); }



        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            var headers = _database.ListHeaders().ToArray();
            Fields.Clear();
            Fields.AddRange(headers.Select(h => h.Name));

            AddExtraField(Fields, NoneField, 0);


            RangeFields.Clear();
            RangeFields.AddRange(headers.Where(h => h.Range).Select(h => h.Name));
            AddExtraField(RangeFields, NoneField, 0);

        }

        private void AddExtraField(ICollection<string> collection, string fieldName, int index)
        {
            if (!Fields.Contains(fieldName))
            {
                if (index >= 0)
                    Fields.Insert(index, fieldName);
                else
                    Fields.Add(fieldName);
            }
        }

        public SearchViewModel(Database database)
        {
            _database = database;
        }

        public DelegateCommand CloseCommand { get => _closeCommand ??= new DelegateCommand(OnClose); }

        private void OnClose()
        {
            RequestClose.Invoke(ButtonResult.Cancel);
        }

        private DelegateCommand _applySearchCommand;
        private string _searchText;
        private string _searchFrom;
        private string _searchTo;

        public DelegateCommand ApplySearchCommand
        {
            get { return _applySearchCommand ??= new DelegateCommand(OnApplySearch); }
        }

        private void OnApplySearch()
        {
            RequestClose.Invoke(new DialogParameters { { "Data", this } }, ButtonResult.OK);
        }
    }
}
