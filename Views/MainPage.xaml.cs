using LSBSteganographyDetector.ViewModels;

namespace LSBSteganographyDetector
{
    public partial class MainPage : ContentPage
    {
        private MainPageViewModel ViewModel => (MainPageViewModel)BindingContext;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = new MainPageViewModel();
        }


    }
} 