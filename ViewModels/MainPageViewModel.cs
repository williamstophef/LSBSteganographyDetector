using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services;
using LSBSteganographyDetector.Services.BatchProcessing;

namespace LSBSteganographyDetector.ViewModels
{
    public class MainPageViewModel : BaseViewModel
    {
        private readonly IStatisticalLSBDetector _detector;
        private readonly IBatchProcessor _batchProcessor;
        
        // Private fields
        private string _selectedImagePath = "";
        private string _selectedFolderPath = "";
        private bool _isBatchMode = false;
        private DetectionResult? _lastResult;
        private string _selectedImageLabel = "";
        private string _progressText = "";
        private bool _showImageInfo = false;
        private bool _showAnalysisSection = false;
        private bool _showResults = false;
        private bool _showProgress = false;
        private bool _showPreviewImage = false;
        private string _previewImageSource = "";
        private string _overallResultText = "";
        private string _overallResultColor = "";
        private string _summaryText = "";
        private string _riskLevel = "";
        private string _riskLevelColor = "";
        private string _confidence = "";
        private string _processingTime = "";
        private string _analyzeButtonText = "🚀 Start Analysis";
        private ObservableCollection<TestResultViewModel> _testResults = new();

        public MainPageViewModel()
        {
            _detector = new StatisticalLSBDetectorRefactored();
            _batchProcessor = new FolderBatchProcessor(_detector);
            Title = "LSB Steganography Detector";
            
            // Initialize commands
            SelectImageCommand = new Command(async () => await SelectImageAsync());
            SelectFolderCommand = new Command(async () => await SelectFolderAsync());
            AnalyzeCommand = new Command(async () => await AnalyzeAsync(), () => CanAnalyze);
            ExportCommand = new Command(async () => await ExportAsync(), () => CanExport);
        }

        #region Properties

        public string SelectedImageLabel
        {
            get => _selectedImageLabel;
            set => SetProperty(ref _selectedImageLabel, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public bool ShowImageInfo
        {
            get => _showImageInfo;
            set => SetProperty(ref _showImageInfo, value);
        }

        public bool ShowAnalysisSection
        {
            get => _showAnalysisSection;
            set => SetProperty(ref _showAnalysisSection, value);
        }

        public bool ShowResults
        {
            get => _showResults;
            set => SetProperty(ref _showResults, value);
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set => SetProperty(ref _showProgress, value);
        }

        public bool ShowPreviewImage
        {
            get => _showPreviewImage;
            set => SetProperty(ref _showPreviewImage, value);
        }

        public string PreviewImageSource
        {
            get => _previewImageSource;
            set => SetProperty(ref _previewImageSource, value);
        }

        public string OverallResultText
        {
            get => _overallResultText;
            set => SetProperty(ref _overallResultText, value);
        }

        public string OverallResultColor
        {
            get => _overallResultColor;
            set => SetProperty(ref _overallResultColor, value);
        }

        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        public string RiskLevel
        {
            get => _riskLevel;
            set => SetProperty(ref _riskLevel, value);
        }

        public string RiskLevelColor
        {
            get => _riskLevelColor;
            set => SetProperty(ref _riskLevelColor, value);
        }

        public string Confidence
        {
            get => _confidence;
            set => SetProperty(ref _confidence, value);
        }

        public string ProcessingTime
        {
            get => _processingTime;
            set => SetProperty(ref _processingTime, value);
        }

        public string AnalyzeButtonText
        {
            get => _analyzeButtonText;
            set => SetProperty(ref _analyzeButtonText, value);
        }

        public ObservableCollection<TestResultViewModel> TestResults
        {
            get => _testResults;
            set => SetProperty(ref _testResults, value);
        }

        // Override IsBusy to trigger command state updates
        public new bool IsBusy
        {
            get => base.IsBusy;
            set
            {
                if (base.IsBusy != value)
                {
                    base.IsBusy = value;
                    UpdateCommandStates();
                }
            }
        }

        public bool CanAnalyze => !IsBusy && (!string.IsNullOrEmpty(_selectedImagePath) || !string.IsNullOrEmpty(_selectedFolderPath));
        
        public bool CanExport => !IsBusy && _lastResult != null;

        #endregion

        #region Commands

        public ICommand SelectImageCommand { get; }
        public ICommand SelectFolderCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand ExportCommand { get; }

        #endregion

        #region Methods

        private async Task SelectImageAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Image File",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.image" } },
                        { DevicePlatform.Android, new[] { "image/*" } },
                        { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff" } },
                        { DevicePlatform.MacCatalyst, new[] { "public.image" } }
                    })
                });

                if (result != null)
                {
                    _isBatchMode = false;
                    await LoadSelectedImage(result);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to select image: {ex.Message}");
            }
        }

        private async Task SelectFolderAsync()
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(CancellationToken.None);

                if (result != null && result.IsSuccessful)
                {
                    _isBatchMode = true;
                    _selectedFolderPath = result.Folder.Path;
                    await LoadSelectedFolder(result.Folder.Path);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to select folder: {ex.Message}");
            }
        }

        private async Task LoadSelectedImage(FileResult file)
        {
            try
            {
                // Copy file to app data directory for processing
                var localFile = Path.Combine(FileSystem.Current.CacheDirectory, "selected_image.png");
                
                using (var sourceStream = await file.OpenReadAsync())
                using (var localFileStream = File.Create(localFile))
                {
                    await sourceStream.CopyToAsync(localFileStream);
                }

                _selectedImagePath = localFile;

                // Update UI
                var fileInfo = new FileInfo(localFile);
                var sizeKB = fileInfo.Length / 1024.0;
                SelectedImageLabel = $"📁 {file.FileName} ({sizeKB:F1} KB)";
                PreviewImageSource = localFile;
                
                ShowImageInfo = true;
                ShowAnalysisSection = true;
                ShowResults = false;
                ShowPreviewImage = true;

                // Update analyze button for single image mode
                AnalyzeButtonText = "🚀 Analyze Image";
                
                UpdateCommandStates();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to load image: {ex.Message}");
            }
        }

        private async Task LoadSelectedFolder(string folderPath)
        {
            try
            {
                // Count images in folder
                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };
                var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();

                // Update UI
                SelectedImageLabel = $"📁 {Path.GetFileName(folderPath)} ({imageFiles.Length} images)";
                PreviewImageSource = ""; // Hide preview for batch mode
                
                ShowImageInfo = true;
                ShowAnalysisSection = true;
                ShowResults = false;
                ShowPreviewImage = false;

                // Update analyze button for batch mode
                AnalyzeButtonText = "🚀 Process Batch";

                if (imageFiles.Length == 0)
                {
                    await ShowErrorAsync("No Images", "The selected folder contains no supported image files.");
                    return;
                }
                
                UpdateCommandStates();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to load folder: {ex.Message}");
            }
        }

        private async Task AnalyzeAsync()
        {
            if (_isBatchMode)
            {
                await ProcessBatchFolderAsync();
            }
            else
            {
                await ProcessSingleImageAsync();
            }
        }

        private async Task ProcessSingleImageAsync()
        {
            if (string.IsNullOrEmpty(_selectedImagePath))
            {
                await ShowErrorAsync("Error", "Please select an image first");
                return;
            }

            try
            {
                IsBusy = true;
                ShowProgress = true;
                ProgressText = "Running statistical analysis...";

                // Run detection
                var result = await _detector.DetectLSBAsync(_selectedImagePath);
                _lastResult = result;

                // Display results
                DisplayResults(result);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Analysis Error", $"Failed to analyze image: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                ShowProgress = false;
                UpdateCommandStates();
            }
        }

        private async Task ProcessBatchFolderAsync()
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                await ShowErrorAsync("Error", "Please select a folder first");
                return;
            }

            try
            {
                IsBusy = true;
                ShowProgress = true;
                ProgressText = "Starting batch analysis...";

                // Debug: Check if folder exists and has images
                if (!Directory.Exists(_selectedFolderPath))
                {
                    await ShowErrorAsync("Error", $"Folder does not exist: {_selectedFolderPath}");
                    return;
                }

                var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };
                var imageFiles = Directory.GetFiles(_selectedFolderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();

                ProgressText = $"Found {imageFiles.Length} images in folder...";

                if (imageFiles.Length == 0)
                {
                    await ShowErrorAsync("No Images", "The selected folder contains no supported image files.");
                    return;
                }

                // Create progress handler
                var progress = new Progress<BatchProcessingProgress>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ProgressText = $"Processing {p.CurrentFile} ({p.ProcessedCount}/{p.TotalCount}) - {p.HighRiskCount} high-risk found";
                        
                        if (!string.IsNullOrEmpty(p.ErrorMessage))
                        {
                            ProgressText += $" - {p.ErrorMessage}";
                        }
                    });
                });

                // Run batch detection
                ProgressText = "Running batch detection...";
                var batchResult = await _batchProcessor.ProcessFolderAsync(_selectedFolderPath, progress);

                // Debug: Check results
                ProgressText = $"Batch completed: {batchResult.ProcessedImages}/{batchResult.TotalImages} processed, " +
                              $"High: {batchResult.HighRiskImages}, Medium: {batchResult.MediumRiskImages}, Clean: {batchResult.LowRiskImages}";

                // Wait a moment to show the final status
                await Task.Delay(1000);

                // Navigate to batch results page
                await Shell.Current.GoToAsync("batchresults", new Dictionary<string, object>
                {
                    { "BatchSummary", batchResult }
                });
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Batch Analysis Error", $"Failed to analyze folder: {ex.Message}\n\nStack trace: {ex.StackTrace}");
            }
            finally
            {
                IsBusy = false;
                ShowProgress = false;
                UpdateCommandStates();
            }
        }

        private void DisplayResults(DetectionResult result)
        {
            // Overall result styling
            if (result.IsSuspicious)
            {
                OverallResultColor = "#ffebee"; // Light red
                OverallResultText = "⚠️ STEGANOGRAPHY DETECTED";
            }
            else
            {
                OverallResultColor = "#e8f5e8"; // Light green
                OverallResultText = "✅ NO STEGANOGRAPHY DETECTED";
            }

            // Update summary information
            SummaryText = result.Summary.Replace("\n", "\n");
            
            // Risk level with color coding
            RiskLevel = result.RiskLevel;
            RiskLevelColor = GetRiskLevelColor(result.RiskLevel);
            
            Confidence = $"{result.OverallConfidence:F1}%";
            ProcessingTime = $"{result.ProcessingTimeMs} ms";

            // Convert test results to view models
            TestResults.Clear();
            
            // Categorize tests by confidence level
            var categorizedTests = CategorizeTestResults(result);
            
            foreach (var category in categorizedTests)
            {
                foreach (var test in category.Tests)
                {
                    TestResults.Add(test);
                }
            }

            ShowResults = true;
            UpdateCommandStates();
        }

        private string GetRiskLevelColor(string riskLevel)
        {
            return riskLevel.ToUpperInvariant() switch
            {
                "HIGH" => "#d32f2f",
                "MEDIUM" => "#f57c00", 
                "LOW" => "#2e7d32",
                _ => "#5d6d7e"
            };
        }

        private List<TestCategory> CategorizeTestResults(DetectionResult result)
        {
            var categories = new List<TestCategory>
            {
                new() { Name = "🚨 HIGH PRIORITY FINDINGS (70%+ Confidence)", Tests = new List<TestResultViewModel>() },
                new() { Name = "⚠️ MEDIUM PRIORITY FINDINGS (40-70% Confidence)", Tests = new List<TestResultViewModel>() },
                new() { Name = "ℹ️ LOW PRIORITY FINDINGS (<40% Confidence)", Tests = new List<TestResultViewModel>() }
            };

            foreach (var test in result.TestResults)
            {
                // Calculate normalized confidence (score relative to threshold)
                var normalizedScore = test.Value.Threshold > 0 ? 
                    Math.Min(test.Value.Score / test.Value.Threshold, 2.0) : 
                    test.Value.Score;
                
                var testVM = new TestResultViewModel(test.Key, test.Value, normalizedScore >= 0.7);
                
                // Categorize by confidence level
                if (normalizedScore >= 0.7) // 70% and above
                {
                    categories[0].Tests.Add(testVM);
                }
                else if (normalizedScore >= 0.4) // 40-69%
                {
                    categories[1].Tests.Add(testVM);
                }
                else // Below 40%
                {
                    categories[2].Tests.Add(testVM);
                }
            }

            // Sort each category by score (highest first) and return only non-empty categories
            return categories
                .Where(c => c.Tests.Any())
                .Select(c => 
                {
                    c.Tests = c.Tests.OrderByDescending(t => t.Score).ToList();
                    return c;
                })
                .ToList();
        }

        private async Task ExportAsync()
        {
            if (_lastResult == null) return;

            try
            {
                var report = GenerateDetailedReport(_lastResult);
                var fileName = $"LSB_Analysis_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                
                await File.WriteAllTextAsync(filePath, report);

                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "LSB Analysis Report",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Export Error", $"Failed to export report: {ex.Message}");
            }
        }

        private string GenerateDetailedReport(DetectionResult result)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=====================================");
            sb.AppendLine("    LSB STEGANOGRAPHY ANALYSIS REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine();
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Image File: {Path.GetFileName(_selectedImagePath)}");
            sb.AppendLine($"Processing Time: {result.ProcessingTimeMs} ms");
            sb.AppendLine();
            
            sb.AppendLine("OVERALL RESULT:");
            sb.AppendLine($"  Detection Status: {(result.IsSuspicious ? "⚠️ STEGANOGRAPHY DETECTED" : "✅ NO STEGANOGRAPHY DETECTED")}");
            sb.AppendLine($"  Risk Level: {result.RiskLevel}");
            sb.AppendLine($"  Confidence: {result.OverallConfidence:F1}%");
            sb.AppendLine();
            
            sb.AppendLine("SUMMARY:");
            sb.AppendLine(result.Summary);
            sb.AppendLine();
            
            var categorizedTests = CategorizeTestResults(result);
            
            foreach (var category in categorizedTests)
            {
                sb.AppendLine(category.Name);
                sb.AppendLine(new string('=', category.Name.Length));
                
                foreach (var test in category.Tests)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Test: {test.TestName}");
                    sb.AppendLine($"Result: {(test.IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL")}");
                    sb.AppendLine($"Score: {test.Score:F4}");
                    sb.AppendLine($"Threshold: {test.Threshold:F4}");
                    sb.AppendLine($"Description: {test.Description}");
                    sb.AppendLine($"Interpretation: {test.Interpretation}");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by LSB Steganography Detector v2.0");
            
            return sb.ToString();
        }

        private void UpdateCommandStates()
        {
            OnPropertyChanged(nameof(CanAnalyze));
            OnPropertyChanged(nameof(CanExport));
            
            // Explicitly notify commands to re-evaluate CanExecute
            ((Command)AnalyzeCommand).ChangeCanExecute();
            ((Command)ExportCommand).ChangeCanExecute();
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }

        #endregion
    }

    #region Helper Classes

    public class TestResultViewModel
    {
        public TestResultViewModel(string testName, TestResult testResult, bool isHighPriority)
        {
            TestName = testResult.TestName;
            Score = testResult.Score;
            Threshold = testResult.Threshold;
            IsSuspicious = testResult.IsSuspicious;
            Description = testResult.Description;
            Interpretation = testResult.Interpretation;
            IsHighPriority = isHighPriority;
        }

        public string TestName { get; }
        public double Score { get; }
        public double Threshold { get; }
        public bool IsSuspicious { get; }
        public string Description { get; }
        public string Interpretation { get; }
        public bool IsHighPriority { get; }

        public string StatusText => IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL";
        public string StatusColor => IsSuspicious ? "#e65100" : "#2e7d32";
        public string BackgroundColor => IsHighPriority 
            ? (IsSuspicious ? "#ffebee" : "#f3e5f5")
            : (IsSuspicious ? "#fff3e0" : "#f1f8e9");
        public string BorderColor => IsHighPriority 
            ? (IsSuspicious ? "#d32f2f" : "#7b1fa2")
            : (IsSuspicious ? "#ff9800" : "#4caf50");
    }

    public class TestCategory
    {
        public string Name { get; set; } = "";
        public List<TestResultViewModel> Tests { get; set; } = new();
    }

    #endregion
} 