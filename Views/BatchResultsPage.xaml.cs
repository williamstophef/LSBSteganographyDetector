using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.ViewModels;

namespace LSBSteganographyDetector.Views
{
    [QueryProperty(nameof(BatchSummary), "BatchSummary")]
    public partial class BatchResultsPage : ContentPage
    {
        private BatchResultsViewModel ViewModel => (BatchResultsViewModel)BindingContext;
        private BatchProcessingSummary? _pendingBatchSummary;

        public BatchProcessingSummary BatchSummary
        {
            set
            {
                if (value != null)
                {
                    if (ViewModel != null)
                    {
                        ViewModel.LoadBatchSummary(value);
                    }
                    else
                    {
                        // Store for later if ViewModel isn't ready yet
                        _pendingBatchSummary = value;
                    }
                }
            }
        }

        public BatchResultsPage()
        {
            InitializeComponent();
            BindingContext = new BatchResultsViewModel();
            
            // Load pending data if it was set before BindingContext was ready
            if (_pendingBatchSummary != null)
            {
                ViewModel.LoadBatchSummary(_pendingBatchSummary);
                _pendingBatchSummary = null;
            }
        }

        public BatchResultsPage(BatchProcessingSummary batchSummary) : this()
        {
            if (ViewModel != null)
            {
                ViewModel.LoadBatchSummary(batchSummary);
            }
        }



        private void OnImageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is BatchDetectionResultViewModel selectedItem)
            {
                ViewModel.ImageSelectedCommand.Execute(selectedItem);
                
                // Clear selection from the sender collection
                if (sender is CollectionView collection)
                {
                    collection.SelectedItem = null;
                }
            }
        }

    }
} 