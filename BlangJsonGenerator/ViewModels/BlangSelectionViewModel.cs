using System;
using ReactiveUI;

namespace BlangJsonGenerator.ViewModels
{
    public class BlangSelectionViewModel : ViewModelBase
    {
        // Array of options to select from
        private string[] _blangOptions = Array.Empty<string>();

        public string[] BlangOptions
        {
            get => _blangOptions;
            set => this.RaiseAndSetIfChanged(ref _blangOptions, value);
        }
    }
}
