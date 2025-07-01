using LSBSteganographyDetector.ViewModels;

namespace LSBSteganographyDetector.Views
{
    public partial class SteganographyToolsPage : ContentPage
    {
        private SteganographyToolsViewModel ViewModel => (SteganographyToolsViewModel)BindingContext;

        public SteganographyToolsPage()
        {
            InitializeComponent();
            BindingContext = new SteganographyToolsViewModel();
        }
    }
} 