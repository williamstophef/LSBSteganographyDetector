using LSBSteganographyDetector.ViewModels;

namespace LSBSteganographyDetector.Views;

public partial class GeotagAnalysisPage : ContentPage
{
    private GeotagAnalysisViewModel ViewModel => (GeotagAnalysisViewModel)BindingContext;

    public GeotagAnalysisPage()
    {
        InitializeComponent();
        
        // Subscribe to ViewModel events for WebView updates
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        // Subscribe to property changes for map updates
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GeotagAnalysisViewModel.MapHtml) && !string.IsNullOrEmpty(ViewModel.MapHtml))
        {
            // Update WebView with map HTML
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapWebView.Source = new HtmlWebViewSource
                {
                    Html = ViewModel.MapHtml
                };
            });
        }
    }

    protected override void OnDisappearing()
    {
        // Clean up event subscriptions
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        
        base.OnDisappearing();
    }
} 