using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.ViewModels
{
    public class BatchResultsViewModel : BaseViewModel
    {
        private BatchProcessingSummary? _batchSummary;
        private string _summaryText = "";
        private string _totalImagesText = "";
        private string _highRiskText = "";
        private string _mediumRiskText = "";
        private string _cleanImagesText = "";
        private string _processingTimeText = "";
        private string _highRiskTabText = "⚠️ High Risk (0)";
        private string _mediumRiskTabText = "⚡ Medium Risk (0)";
        private string _cleanTabText = "✅ Clean (0)";
        private string _highRiskDescriptionText = "";
        private string _activeTab = "high";
        private bool _showHighRiskContent = true;
        private bool _showMediumRiskContent = false;
        private bool _showCleanContent = false;
        private bool _showEmptyHighRisk = false;
        private bool _showEmptyMediumRisk = false;
        private bool _showEmptyClean = false;

        public BatchResultsViewModel()
        {
            Title = "Batch Results";
            
            HighRiskImages = new ObservableCollection<BatchDetectionResultViewModel>();
            MediumRiskImages = new ObservableCollection<BatchDetectionResultViewModel>();
            CleanImages = new ObservableCollection<BatchDetectionResultViewModel>();
            
            // Initialize commands
            ShowHighRiskTabCommand = new Command(() => ShowHighRiskTab());
            ShowMediumRiskTabCommand = new Command(() => ShowMediumRiskTab());
            ShowCleanTabCommand = new Command(() => ShowCleanTab());
            ExportReportCommand = new Command(async () => await ExportReportAsync());
            BackToMainCommand = new Command(async () => await BackToMainAsync());
            ProcessAnotherFolderCommand = new Command(async () => await ProcessAnotherFolderAsync());
            ImageSelectedCommand = new Command<BatchDetectionResultViewModel>(async (item) => await ShowImageDetailsAsync(item));
        }

        #region Properties

        public ObservableCollection<BatchDetectionResultViewModel> HighRiskImages { get; }
        public ObservableCollection<BatchDetectionResultViewModel> MediumRiskImages { get; }
        public ObservableCollection<BatchDetectionResultViewModel> CleanImages { get; }

        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public string TotalImagesText
        {
            get => _totalImagesText;
            set => SetProperty(ref _totalImagesText, value);
        }

        public string HighRiskText
        {
            get => _highRiskText;
            set => SetProperty(ref _highRiskText, value);
        }

        public string MediumRiskText
        {
            get => _mediumRiskText;
            set => SetProperty(ref _mediumRiskText, value);
        }

        public string CleanImagesText
        {
            get => _cleanImagesText;
            set => SetProperty(ref _cleanImagesText, value);
        }

        public string ProcessingTimeText
        {
            get => _processingTimeText;
            set => SetProperty(ref _processingTimeText, value);
        }

        public string HighRiskTabText
        {
            get => _highRiskTabText;
            set => SetProperty(ref _highRiskTabText, value);
        }

        public string MediumRiskTabText
        {
            get => _mediumRiskTabText;
            set => SetProperty(ref _mediumRiskTabText, value);
        }

        public string CleanTabText
        {
            get => _cleanTabText;
            set => SetProperty(ref _cleanTabText, value);
        }

        public string HighRiskDescriptionText
        {
            get => _highRiskDescriptionText;
            set => SetProperty(ref _highRiskDescriptionText, value);
        }

        public bool ShowHighRiskContent
        {
            get => _showHighRiskContent;
            set => SetProperty(ref _showHighRiskContent, value);
        }

        public bool ShowMediumRiskContent
        {
            get => _showMediumRiskContent;
            set => SetProperty(ref _showMediumRiskContent, value);
        }

        public bool ShowCleanContent
        {
            get => _showCleanContent;
            set => SetProperty(ref _showCleanContent, value);
        }

        public bool ShowEmptyHighRisk
        {
            get => _showEmptyHighRisk;
            set => SetProperty(ref _showEmptyHighRisk, value);
        }

        public bool ShowEmptyMediumRisk
        {
            get => _showEmptyMediumRisk;
            set => SetProperty(ref _showEmptyMediumRisk, value);
        }

        public bool ShowEmptyClean
        {
            get => _showEmptyClean;
            set => SetProperty(ref _showEmptyClean, value);
        }

        public string ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        #endregion

        #region Commands

        public ICommand ShowHighRiskTabCommand { get; }
        public ICommand ShowMediumRiskTabCommand { get; }
        public ICommand ShowCleanTabCommand { get; }
        public ICommand ExportReportCommand { get; }
        public ICommand BackToMainCommand { get; }
        public ICommand ProcessAnotherFolderCommand { get; }
        public ICommand ImageSelectedCommand { get; }

        #endregion

        #region Methods

        public void LoadBatchSummary(BatchProcessingSummary batchSummary)
        {
            _batchSummary = batchSummary;
            
            // Update summary information
            var duration = _batchSummary.EndTime - _batchSummary.StartTime;
            SummaryText = $"Analyzed {_batchSummary.ProcessedImages} images in {duration.TotalSeconds:F1} seconds";

            // Update statistics
            TotalImagesText = _batchSummary.TotalImages.ToString();
            HighRiskText = _batchSummary.HighRiskImages.ToString();
            MediumRiskText = _batchSummary.MediumRiskImages.ToString();
            CleanImagesText = _batchSummary.LowRiskImages.ToString();
            ProcessingTimeText = $"{_batchSummary.TotalProcessingTimeMs} ms";

            // Load collections
            LoadHighRiskImages();
            LoadMediumRiskImages();
            LoadCleanImages();

            // Update tab descriptions
            UpdateTabDescriptions();
        }

        private void LoadHighRiskImages()
        {
            HighRiskImages.Clear();
            if (_batchSummary?.HighRiskResults != null)
            {
                foreach (var result in _batchSummary.HighRiskResults)
                {
                    HighRiskImages.Add(new BatchDetectionResultViewModel(result));
                }
            }
            
            ShowEmptyHighRisk = _batchSummary?.HighRiskImages == 0;
        }

        private void LoadMediumRiskImages()
        {
            MediumRiskImages.Clear();
            if (_batchSummary?.MediumRiskResults != null)
            {
                foreach (var result in _batchSummary.MediumRiskResults)
                {
                    MediumRiskImages.Add(new BatchDetectionResultViewModel(result));
                }
            }
            
            ShowEmptyMediumRisk = _batchSummary?.MediumRiskImages == 0;
        }

        private void LoadCleanImages()
        {
            CleanImages.Clear();
            if (_batchSummary?.LowRiskResults != null)
            {
                foreach (var result in _batchSummary.LowRiskResults)
                {
                    CleanImages.Add(new BatchDetectionResultViewModel(result));
                }
            }
            
            ShowEmptyClean = _batchSummary?.LowRiskImages == 0;
        }

        private void UpdateTabDescriptions()
        {
            if (_batchSummary == null) return;
            
            HighRiskDescriptionText = _batchSummary.HighRiskImages == 0 
                ? "🎉 Excellent! No high-risk images were detected."
                : $"Found {_batchSummary.HighRiskImages} image(s) with high steganography risk. Tap for details.";
                
            // Update tab button text with counts
            HighRiskTabText = $"⚠️ High Risk ({_batchSummary.HighRiskImages})";
            MediumRiskTabText = $"⚡ Medium Risk ({_batchSummary.MediumRiskImages})";
            CleanTabText = $"✅ Clean ({_batchSummary.LowRiskImages})";
        }

        private void ShowHighRiskTab()
        {
            ShowHighRiskContent = true;
            ShowMediumRiskContent = false;
            ShowCleanContent = false;
            ActiveTab = "high";
        }

        private void ShowMediumRiskTab()
        {
            ShowHighRiskContent = false;
            ShowMediumRiskContent = true;
            ShowCleanContent = false;
            ActiveTab = "medium";
        }

        private void ShowCleanTab()
        {
            ShowHighRiskContent = false;
            ShowMediumRiskContent = false;
            ShowCleanContent = true;
            ActiveTab = "clean";
        }

        private async Task ShowImageDetailsAsync(BatchDetectionResultViewModel? imageResult)
        {
            if (imageResult == null) return;

            var details = GenerateImageDetails(imageResult);
            
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert("Image Analysis Details", details, "OK");
            }
        }

        private string GenerateImageDetails(BatchDetectionResultViewModel imageResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📄 File: {imageResult.FileName}");
            sb.AppendLine($"📏 Size: {imageResult.FileSizeFormatted}");
            sb.AppendLine($"⚠️ Risk Level: {imageResult.Result.RiskLevel}");
            sb.AppendLine($"🎯 Confidence: {imageResult.ConfidenceFormatted}");
            sb.AppendLine($"⏱️ Processed: {imageResult.ProcessedAt:HH:mm:ss}");
            sb.AppendLine();

            // Categorize tests by confidence level
            var highPriorityTests = new List<(string, TestResult)>();
            var mediumPriorityTests = new List<(string, TestResult)>();
            var lowPriorityTests = new List<(string, TestResult)>();

            foreach (var test in imageResult.Result.TestResults)
            {
                var normalizedScore = test.Value.Threshold > 0 ? 
                    Math.Min(test.Value.Score / test.Value.Threshold, 2.0) : 
                    test.Value.Score;
                
                if (normalizedScore >= 0.7)
                {
                    highPriorityTests.Add((test.Key, test.Value));
                }
                else if (normalizedScore >= 0.4)
                {
                    mediumPriorityTests.Add((test.Key, test.Value));
                }
                else
                {
                    lowPriorityTests.Add((test.Key, test.Value));
                }
            }

            // Display categorized results
            if (highPriorityTests.Any())
            {
                sb.AppendLine("🚨 HIGH PRIORITY FINDINGS (70%+ Confidence):");
                foreach (var (testName, testResult) in highPriorityTests.OrderByDescending(t => t.Item2.Score))
                {
                    var status = testResult.IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL";
                    sb.AppendLine($"  • {testResult.TestName}: {status}");
                    sb.AppendLine($"    Score: {testResult.Score:F4} (Threshold: {testResult.Threshold:F4})");
                }
                sb.AppendLine();
            }

            if (mediumPriorityTests.Any())
            {
                sb.AppendLine("⚠️ MEDIUM PRIORITY FINDINGS (40-70% Confidence):");
                foreach (var (testName, testResult) in mediumPriorityTests.OrderByDescending(t => t.Item2.Score))
                {
                    var status = testResult.IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL";
                    sb.AppendLine($"  • {testResult.TestName}: {status}");
                    sb.AppendLine($"    Score: {testResult.Score:F4} (Threshold: {testResult.Threshold:F4})");
                }
                sb.AppendLine();
            }

            if (lowPriorityTests.Any())
            {
                sb.AppendLine("ℹ️ LOW PRIORITY FINDINGS (<40% Confidence):");
                foreach (var (testName, testResult) in lowPriorityTests.OrderByDescending(t => t.Item2.Score))
                {
                    var status = testResult.IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL";
                    sb.AppendLine($"  • {testResult.TestName}: {status}");
                    sb.AppendLine($"    Score: {testResult.Score:F4} (Threshold: {testResult.Threshold:F4})");
                }
            }

            return sb.ToString();
        }

        private async Task ExportReportAsync()
        {
            if (_batchSummary == null) return;

            try
            {
                var report = GenerateBatchReport();
                var fileName = $"LSB_Batch_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                
                await File.WriteAllTextAsync(filePath, report);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "LSB Batch Analysis Report",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
                }
            }
        }

        private string GenerateBatchReport()
        {
            if (_batchSummary == null) return "";
            
            var sb = new StringBuilder();
            
            sb.AppendLine("=====================================");
            sb.AppendLine("    LSB STEGANOGRAPHY BATCH ANALYSIS REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine();
            sb.AppendLine($"Analysis Date: {_batchSummary.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Processing Duration: {(_batchSummary.EndTime - _batchSummary.StartTime).TotalSeconds:F1} seconds");
            sb.AppendLine($"Total Processing Time: {_batchSummary.TotalProcessingTimeMs} ms");
            sb.AppendLine();
            
            sb.AppendLine("BATCH SUMMARY:");
            sb.AppendLine($"  Total Images Processed: {_batchSummary.ProcessedImages} / {_batchSummary.TotalImages}");
            sb.AppendLine($"  High Risk Images: {_batchSummary.HighRiskImages}");
            sb.AppendLine($"  Medium Risk Images: {_batchSummary.MediumRiskImages}");
            sb.AppendLine($"  Clean Images: {_batchSummary.LowRiskImages}");
            
            if (_batchSummary.HighRiskImages > 0)
            {
                var riskPercentage = (double)_batchSummary.HighRiskImages / _batchSummary.ProcessedImages * 100;
                sb.AppendLine($"  Risk Rate: {riskPercentage:F1}%");
            }
            
            sb.AppendLine();
            
            if (_batchSummary.HighRiskImages > 0)
            {
                sb.AppendLine("HIGH RISK IMAGES:");
                sb.AppendLine("=====================================");
                
                foreach (var result in _batchSummary.HighRiskResults)
                {
                    sb.AppendLine();
                    sb.AppendLine($"📄 File: {result.FileName}");
                    sb.AppendLine($"   Path: {result.ImagePath}");
                    sb.AppendLine($"   Size: {result.FileSizeBytes / 1024.0:F1} KB");
                    sb.AppendLine($"   Risk Level: {result.Result.RiskLevel}");
                    sb.AppendLine($"   Confidence: {result.Result.OverallConfidence:F1}%");
                    sb.AppendLine($"   Processed: {result.ProcessedAt:yyyy-MM-dd HH:mm:ss}");
                    
                    sb.AppendLine("   Suspicious Tests:");
                    foreach (var test in result.Result.TestResults.Where(t => t.Value.IsSuspicious))
                    {
                        sb.AppendLine($"     • {test.Value.TestName}: {test.Value.Score:F4}");
                    }
                }
            }
            else
            {
                sb.AppendLine("🎉 EXCELLENT RESULTS!");
                sb.AppendLine("No high-risk images detected in this batch.");
                sb.AppendLine("All analyzed images appear to be clean and free from steganographic content.");
            }
            
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by LSB Steganography Detector v2.0");
            sb.AppendLine("Batch Analysis Engine");
            
            return sb.ToString();
        }

        private async Task BackToMainAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task ProcessAnotherFolderAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        #endregion
    }

    // ViewModel class for individual batch results
    public class BatchDetectionResultViewModel
    {
        private readonly BatchDetectionResult _result;

        public BatchDetectionResultViewModel(BatchDetectionResult result)
        {
            _result = result;
        }

        public string FileName => _result.FileName;
        public string ImagePath => _result.ImagePath;
        public DetectionResult Result => _result.Result;
        public DateTime ProcessedAt => _result.ProcessedAt;

        public string FileSizeFormatted
        {
            get
            {
                var sizeKB = _result.FileSizeBytes / 1024.0;
                return sizeKB < 1024 ? $"{sizeKB:F1} KB" : $"{sizeKB / 1024:F1} MB";
            }
        }

        public string ConfidenceFormatted => $"{_result.Result.OverallConfidence:F1}%";

        public string SuspiciousTestsText
        {
            get
            {
                var suspiciousTests = _result.Result.TestResults
                    .Where(t => t.Value.IsSuspicious)
                    .Select(t => t.Key)
                    .ToList();
                
                return suspiciousTests.Count > 0 
                    ? $"Suspicious: {string.Join(", ", suspiciousTests)}"
                    : "No suspicious tests";
            }
        }
    }
} 