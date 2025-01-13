using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using william_sku.Data;

namespace william_sku.ViewModels
{
    internal class SettingsViewModel : BindableBase, IDialogAware
    {
        private readonly Database _database;

        public ObservableCollection<string> Headers { get; set; } = new();
        public DialogCloseListener RequestClose { get; }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Task.Run(FetchHeaders);
        }

        private void FetchHeaders()
        {
            var headers = _database.ListHeaders();

            Headers.AddRange(headers);
        }

        public SettingsViewModel(Database database)
        {
            _database = database;
        }
    }
}
