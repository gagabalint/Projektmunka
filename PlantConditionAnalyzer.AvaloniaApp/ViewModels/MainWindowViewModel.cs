using PlantConditionAnalyzer.Core.Interfaces;

namespace PlantConditionAnalyzer.AvaloniaApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IImageProcessingService _imageProcessor;

        // A 'Greeting' egy új tulajdonság, amit a 'ViewModelBase'-ből örökölt
        // 'ObservableObject' fog kezelni. A sablonodban a 'Greeting'
        // helyett lehet, hogy más van, azt átírhatod.
        public string Greeting { get; }

        public MainWindowViewModel(IImageProcessingService imageProcessor)
        {
            _imageProcessor = imageProcessor;

            if (_imageProcessor != null)
            {
                Greeting = "Sikeres Indítás! A DI konténer működik.";
            }
            else
            {
                Greeting = "Hiba! A DI nem tudta beadni a ImageProcessingService-t.";
            }
        }
    }
}
