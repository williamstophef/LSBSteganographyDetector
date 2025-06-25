using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services;

namespace LSBSteganographyDetector.Views
{
    [QueryProperty(nameof(BatchSummary), "BatchSummary")]
    public partial class BatchResultsPage : ContentPage
    {
        private BatchProcessingSummary _batchSummary = null!;
        private ObservableCollection<BatchDetectionResultViewModel> _highRiskImages;
        private ObservableCollection<BatchDetectionResultViewModel> _mediumRiskImages;
        private ObservableCollection<BatchDetectionResultViewModel> _cleanImages;

        public BatchProcessingSummary BatchSummary
        {
            set
            {
                _batchSummary = value;
                if (_batchSummary != null)
                {
                    LoadResults();
                }
            }
        }

        public BatchResultsPage()
        {
            InitializeComponent();
            _highRiskImages = new ObservableCollection<BatchDetectionResultViewModel>();
            _mediumRiskImages = new ObservableCollection<BatchDetectionResultViewModel>();
            _cleanImages = new ObservableCollection<BatchDetectionResultViewModel>();
        }

        public BatchResultsPage(BatchProcessingSummary batchSummary) : this()
        {
            _batchSummary = batchSummary;
            LoadResults();
        }

        private void LoadResults()
        {
            // Update summary label
            var duration = _batchSummary.EndTime - _batchSummary.StartTime;
            SummaryLabel.Text = $"Analyzed {_batchSummary.ProcessedImages} images in {duration.TotalSeconds:F1} seconds";

            // Update statistics
            TotalImagesLabel.Text = _batchSummary.TotalImages.ToString();
            HighRiskLabel.Text = _batchSummary.HighRiskImages.ToString();
            MediumRiskLabel.Text = _batchSummary.MediumRiskImages.ToString();
            CleanImagesLabel.Text = _batchSummary.LowRiskImages.ToString();
            ProcessingTimeLabel.Text = $"{_batchSummary.TotalProcessingTimeMs} ms";

            // Load all image collections
            LoadHighRiskImages();
            LoadMediumRiskImages();
            LoadCleanImages();

            // Set default tab descriptions
            UpdateTabDescriptions();
        }

        private void LoadHighRiskImages()
        {
            _highRiskImages.Clear();
            foreach (var result in _batchSummary.HighRiskResults)
            {
                _highRiskImages.Add(new BatchDetectionResultViewModel(result));
            }
            HighRiskImagesCollection.ItemsSource = _highRiskImages;
            
            // Handle empty state
            EmptyHighRiskPanel.IsVisible = _batchSummary.HighRiskImages == 0;
            HighRiskImagesCollection.IsVisible = _batchSummary.HighRiskImages > 0;
        }

        private void LoadMediumRiskImages()
        {
            _mediumRiskImages.Clear();
            foreach (var result in _batchSummary.MediumRiskResults)
            {
                _mediumRiskImages.Add(new BatchDetectionResultViewModel(result));
            }
            MediumRiskImagesCollection.ItemsSource = _mediumRiskImages;
            
            // Handle empty state
            EmptyMediumRiskPanel.IsVisible = _batchSummary.MediumRiskImages == 0;
            MediumRiskImagesCollection.IsVisible = _batchSummary.MediumRiskImages > 0;
        }

        private void LoadCleanImages()
        {
            _cleanImages.Clear();
            foreach (var result in _batchSummary.LowRiskResults)
            {
                _cleanImages.Add(new BatchDetectionResultViewModel(result));
            }
            CleanImagesCollection.ItemsSource = _cleanImages;
            
            // Handle empty state
            EmptyCleanPanel.IsVisible = _batchSummary.LowRiskImages == 0;
            CleanImagesCollection.IsVisible = _batchSummary.LowRiskImages > 0;
        }

        private void UpdateTabDescriptions()
        {
            HighRiskDescriptionLabel.Text = _batchSummary.HighRiskImages == 0 
                ? "🎉 Excellent! No high-risk images were detected."
                : $"Found {_batchSummary.HighRiskImages} image(s) with high steganography risk. Tap for details.";
                
            // Update tab button text with counts
            HighRiskTab.Text = $"⚠️ High Risk ({_batchSummary.HighRiskImages})";
            MediumRiskTab.Text = $"⚡ Medium Risk ({_batchSummary.MediumRiskImages})";
            CleanTab.Text = $"✅ Clean ({_batchSummary.LowRiskImages})";
        }

        // Tab switching methods
        private void OnHighRiskTabClicked(object sender, EventArgs e)
        {
            ShowHighRiskTab();
        }

        private void OnMediumRiskTabClicked(object sender, EventArgs e)
        {
            ShowMediumRiskTab();
        }

        private void OnCleanTabClicked(object sender, EventArgs e)
        {
            ShowCleanTab();
        }

        private void ShowHighRiskTab()
        {
            HighRiskContent.IsVisible = true;
            MediumRiskContent.IsVisible = false;
            CleanContent.IsVisible = false;
            
            // Update tab appearances
            UpdateTabAppearance("high");
        }

        private void ShowMediumRiskTab()
        {
            HighRiskContent.IsVisible = false;
            MediumRiskContent.IsVisible = true;
            CleanContent.IsVisible = false;
            
            // Update tab appearances
            UpdateTabAppearance("medium");
        }

        private void ShowCleanTab()
        {
            HighRiskContent.IsVisible = false;
            MediumRiskContent.IsVisible = false;
            CleanContent.IsVisible = true;
            
            // Update tab appearances
            UpdateTabAppearance("clean");
        }

        private void UpdateTabAppearance(string activeTab)
        {
            // Update button opacity to show active state
            HighRiskTab.Opacity = activeTab == "high" ? 1.0 : 0.7;
            MediumRiskTab.Opacity = activeTab == "medium" ? 1.0 : 0.7;
            CleanTab.Opacity = activeTab == "clean" ? 1.0 : 0.7;
        }

        private async void OnImageSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is BatchDetectionResultViewModel selectedItem)
            {
                await ShowImageDetails(selectedItem);
                
                // Clear selection from all collections
                HighRiskImagesCollection.SelectedItem = null;
                MediumRiskImagesCollection.SelectedItem = null;
                CleanImagesCollection.SelectedItem = null;
            }
        }

        private async Task ShowImageDetails(BatchDetectionResultViewModel imageResult)
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
                // Calculate normalized confidence (score relative to threshold)
                var normalizedScore = test.Value.Threshold > 0 ? 
                    Math.Min(test.Value.Score / test.Value.Threshold, 2.0) : // Cap at 200%
                    test.Value.Score;
                
                // Categorize by confidence level
                if (normalizedScore >= 0.7) // 70% and above
                {
                    highPriorityTests.Add((test.Key, test.Value));
                }
                else if (normalizedScore >= 0.4) // 40-69%
                {
                    mediumPriorityTests.Add((test.Key, test.Value));
                }
                else // Below 40%
                {
                    lowPriorityTests.Add((test.Key, test.Value));
                }
            }

            // Display High Priority Section
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

            // Display Medium Priority Section
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

            // Display Low Priority Section
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

            await DisplayAlert("Image Analysis Details", sb.ToString(), "OK");
        }

        private async void OnExportReportClicked(object sender, EventArgs e)
        {
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
                await DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
            }
        }

        private async void OnBackToMainClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnProcessAnotherFolderClicked(object sender, EventArgs e)
        {
            // Navigate back to main page and trigger folder selection
            await Shell.Current.GoToAsync("..");
            
            // You could also pass a parameter to auto-trigger folder selection
            // This would require modifying the MainPage to handle the parameter
        }

        private string GenerateBatchReport()
        {
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
                    sb.AppendLine($"   Confidence: {result.Result.OverallConfidence:P1}");
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
            sb.AppendLine("Report generated by LSB Steganography Detector v1.0");
            sb.AppendLine("Batch Analysis Engine");
            
            return sb.ToString();
        }
    }

    // ViewModel class for data binding
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

        public string ConfidenceFormatted => $"{_result.Result.OverallConfidence:P1}";

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