using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services;
using CommunityToolkit.Maui.Storage;

namespace LSBSteganographyDetector
{
    public partial class MainPage : ContentPage
    {
        private readonly StatisticalLSBDetector _detector;
        private string _selectedImagePath = "";
        private string _selectedFolderPath = "";
        private bool _isBatchMode = false;
        private DetectionResult? _lastResult;

        public MainPage()
        {
            InitializeComponent();
            _detector = new StatisticalLSBDetector();
        }

        private async void OnSelectImageClicked(object sender, EventArgs e)
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
                await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
            }
        }

        private async void OnSelectFolderClicked(object sender, EventArgs e)
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
                await DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
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
                SelectedImageLabel.Text = $"📁 {file.FileName}";
                PreviewImage.Source = ImageSource.FromFile(localFile);
                
                ImageInfoPanel.IsVisible = true;
                AnalysisFrame.IsVisible = true;
                ResultsFrame.IsVisible = false;

                // Get file size for display
                var fileInfo = new FileInfo(localFile);
                var sizeKB = fileInfo.Length / 1024.0;
                SelectedImageLabel.Text += $" ({sizeKB:F1} KB)";

                // Update analyze button for single image mode
                AnalyzeButton.Text = "🚀 Analyze Image";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load image: {ex.Message}", "OK");
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
                SelectedImageLabel.Text = $"📁 {Path.GetFileName(folderPath)} ({imageFiles.Length} images)";
                PreviewImage.Source = null; // Hide preview for batch mode
                
                ImageInfoPanel.IsVisible = true;
                AnalysisFrame.IsVisible = true;
                ResultsFrame.IsVisible = false;

                // Update analyze button for batch mode
                AnalyzeButton.Text = "🚀 Process Batch";

                if (imageFiles.Length == 0)
                {
                    await DisplayAlert("No Images", "The selected folder contains no supported image files.", "OK");
                    return;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load folder: {ex.Message}", "OK");
            }
        }

        private async void OnAnalyzeClicked(object sender, EventArgs e)
        {
            if (_isBatchMode)
            {
                await ProcessBatchFolder();
            }
            else
            {
                await ProcessSingleImage();
            }
        }

        private async Task ProcessSingleImage()
        {
            if (string.IsNullOrEmpty(_selectedImagePath))
            {
                await DisplayAlert("Error", "Please select an image first", "OK");
                return;
            }

            try
            {
                // Show progress
                AnalyzeButton.IsEnabled = false;
                ProgressPanel.IsVisible = true;
                ProgressIndicator.IsRunning = true;
                ProgressLabel.Text = "Running statistical analysis...";

                // Run detection
                var result = await _detector.DetectLSBAsync(_selectedImagePath);
                _lastResult = result;

                // Hide progress
                ProgressIndicator.IsRunning = false;
                ProgressPanel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;

                // Display results
                await DisplayResults(result);
            }
            catch (Exception ex)
            {
                ProgressIndicator.IsRunning = false;
                ProgressPanel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;
                
                await DisplayAlert("Analysis Error", $"Failed to analyze image: {ex.Message}", "OK");
            }
        }

        private async Task ProcessBatchFolder()
        {
            if (string.IsNullOrEmpty(_selectedFolderPath))
            {
                await DisplayAlert("Error", "Please select a folder first", "OK");
                return;
            }

            try
            {
                // Show progress
                AnalyzeButton.IsEnabled = false;
                ProgressPanel.IsVisible = true;
                ProgressIndicator.IsRunning = true;
                ProgressLabel.Text = "Starting batch analysis...";

                // Create progress handler
                var progress = new Progress<BatchProcessingProgress>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ProgressLabel.Text = $"Processing {p.CurrentFile} ({p.ProcessedCount}/{p.TotalCount}) - {p.HighRiskCount} high-risk found";
                        
                        if (!string.IsNullOrEmpty(p.ErrorMessage))
                        {
                            ProgressLabel.Text += $" - {p.ErrorMessage}";
                        }
                    });
                });

                // Run batch detection
                var batchResult = await _detector.DetectLSBBatchAsync(_selectedFolderPath, progress);

                // Hide progress
                ProgressIndicator.IsRunning = false;
                ProgressPanel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;

                // Navigate to batch results page
                await Shell.Current.GoToAsync("batchresults", new Dictionary<string, object>
                {
                    { "BatchSummary", batchResult }
                });
            }
            catch (Exception ex)
            {
                ProgressIndicator.IsRunning = false;
                ProgressPanel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;
                
                await DisplayAlert("Batch Analysis Error", $"Failed to analyze folder: {ex.Message}", "OK");
            }
        }

        private async Task DisplayResults(DetectionResult result)
        {
            // Overall result styling
            if (result.IsSuspicious)
            {
                OverallResultFrame.BackgroundColor = Color.FromArgb("#ffebee"); // Light red
                OverallResultLabel.Text = "⚠️ STEGANOGRAPHY DETECTED";
                OverallResultLabel.TextColor = Color.FromArgb("#d32f2f");
            }
            else
            {
                OverallResultFrame.BackgroundColor = Color.FromArgb("#e8f5e8"); // Light green
                OverallResultLabel.Text = "✅ NO STEGANOGRAPHY DETECTED";
                OverallResultLabel.TextColor = Color.FromArgb("#2e7d32");
            }

            // Update summary information
            SummaryLabel.Text = result.Summary.Replace("\n", "\n");
            
            // Risk level with color coding
            RiskLevelLabel.Text = result.RiskLevel;
            RiskLevelLabel.TextColor = GetRiskLevelColor(result.RiskLevel);
            
            ConfidenceLabel.Text = $"{result.OverallConfidence:P1}";
            ProcessingTimeLabel.Text = $"{result.ProcessingTimeMs} ms";

            // Clear previous detailed results
            DetailedResultsPanel.Children.Clear();

            // Categorize tests by confidence level
            DisplayCategorizedResults(result);

            // Show results section
            ResultsFrame.IsVisible = true;

            // Auto-scroll to results
            await Task.Delay(100);
            var scrollView = this.FindByName<ScrollView>("ScrollView");
            if (scrollView != null)
            {
                await scrollView.ScrollToAsync(ResultsFrame, ScrollToPosition.Start, true);
            }
        }

        private Frame CreateTestResultFrame(string testName, TestResult test, bool isHighPriority = false)
        {
            Color backgroundColor, borderColor;
            
            if (isHighPriority)
            {
                // High priority gets more attention-grabbing colors
                backgroundColor = test.IsSuspicious ? Color.FromArgb("#ffebee") : Color.FromArgb("#f3e5f5");
                borderColor = test.IsSuspicious ? Color.FromArgb("#d32f2f") : Color.FromArgb("#7b1fa2");
            }
            else
            {
                // Standard colors for medium/low priority
                backgroundColor = test.IsSuspicious ? Color.FromArgb("#fff3e0") : Color.FromArgb("#f1f8e9");
                borderColor = test.IsSuspicious ? Color.FromArgb("#ff9800") : Color.FromArgb("#4caf50");
            }
            
            var frame = new Frame
            {
                BackgroundColor = backgroundColor,
                CornerRadius = 8,
                Padding = isHighPriority ? 18 : 15, // Slightly more padding for high priority
                HasShadow = isHighPriority, // Shadow only for high priority
                BorderColor = borderColor,
                Margin = new Thickness(0, 5)
            };

            var mainStack = new StackLayout { Spacing = 8 };

            // Test name and result
            var headerStack = new StackLayout 
            { 
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.Fill
            };

            var nameLabel = new Label
            {
                Text = test.TestName,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#2c3e50"),
                HorizontalOptions = LayoutOptions.Start
            };

            var statusLabel = new Label
            {
                Text = test.IsSuspicious ? "⚠️ SUSPICIOUS" : "✅ NORMAL",
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = test.IsSuspicious ? Color.FromArgb("#e65100") : Color.FromArgb("#2e7d32"),
                HorizontalOptions = LayoutOptions.End
            };

            headerStack.Children.Add(nameLabel);
            headerStack.Children.Add(statusLabel);
            mainStack.Children.Add(headerStack);

            // Score information
            var scoreStack = new StackLayout 
            { 
                Orientation = StackOrientation.Horizontal,
                Spacing = 15
            };

            scoreStack.Children.Add(new Label
            {
                Text = $"Score: {test.Score:F4}",
                FontSize = 12,
                TextColor = Color.FromArgb("#5d6d7e")
            });

            scoreStack.Children.Add(new Label
            {
                Text = $"Threshold: {test.Threshold:F4}",
                FontSize = 12,
                TextColor = Color.FromArgb("#5d6d7e")
            });

            mainStack.Children.Add(scoreStack);

            // Description
            mainStack.Children.Add(new Label
            {
                Text = test.Description,
                FontSize = 11,
                TextColor = Color.FromArgb("#7f8c8d"),
                FontAttributes = FontAttributes.Italic
            });

            // Interpretation
            mainStack.Children.Add(new Label
            {
                Text = test.Interpretation,
                FontSize = 12,
                TextColor = Color.FromArgb("#34495e")
            });

            frame.Content = mainStack;
            return frame;
        }

        private void DisplayCategorizedResults(DetectionResult result)
        {
            // Categorize tests by their normalized confidence score
            var highPriorityTests = new List<(string, TestResult)>();
            var mediumPriorityTests = new List<(string, TestResult)>();
            var lowPriorityTests = new List<(string, TestResult)>();

            foreach (var test in result.TestResults)
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

            // Sort each category by score (highest first)
            highPriorityTests.Sort((a, b) => b.Item2.Score.CompareTo(a.Item2.Score));
            mediumPriorityTests.Sort((a, b) => b.Item2.Score.CompareTo(a.Item2.Score));
            lowPriorityTests.Sort((a, b) => b.Item2.Score.CompareTo(a.Item2.Score));

            // Display High Priority Section
            if (highPriorityTests.Any())
            {
                DetailedResultsPanel.Children.Add(CreateSectionHeader("🚨 HIGH PRIORITY FINDINGS (70%+ Confidence)", "#d32f2f", "#ffebee"));
                foreach (var (testName, testResult) in highPriorityTests)
                {
                    var testFrame = CreateTestResultFrame(testName, testResult, true);
                    DetailedResultsPanel.Children.Add(testFrame);
                }
                DetailedResultsPanel.Children.Add(CreateSectionSpacer());
            }

            // Display Medium Priority Section
            if (mediumPriorityTests.Any())
            {
                DetailedResultsPanel.Children.Add(CreateSectionHeader("⚠️ MEDIUM PRIORITY FINDINGS (40-70% Confidence)", "#f57c00", "#fff8e1"));
                foreach (var (testName, testResult) in mediumPriorityTests)
                {
                    var testFrame = CreateTestResultFrame(testName, testResult, false);
                    DetailedResultsPanel.Children.Add(testFrame);
                }
                DetailedResultsPanel.Children.Add(CreateSectionSpacer());
            }

            // Display Low Priority Section
            if (lowPriorityTests.Any())
            {
                DetailedResultsPanel.Children.Add(CreateSectionHeader("ℹ️ LOW PRIORITY FINDINGS (<40% Confidence)", "#2e7d32", "#e8f5e8"));
                foreach (var (testName, testResult) in lowPriorityTests)
                {
                    var testFrame = CreateTestResultFrame(testName, testResult, false);
                    DetailedResultsPanel.Children.Add(testFrame);
                }
            }
        }

        private Frame CreateSectionHeader(string title, string textColor, string backgroundColor)
        {
            var headerFrame = new Frame
            {
                BackgroundColor = Color.FromArgb(backgroundColor),
                BorderColor = Color.FromArgb(textColor),
                CornerRadius = 8,
                Padding = 12,
                HasShadow = false,
                Margin = new Thickness(0, 10, 0, 5)
            };

            var headerLabel = new Label
            {
                Text = title,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = Color.FromArgb(textColor),
                HorizontalTextAlignment = TextAlignment.Center
            };

            headerFrame.Content = headerLabel;
            return headerFrame;
        }

        private BoxView CreateSectionSpacer()
        {
            return new BoxView
            {
                Color = Color.FromArgb("#e0e0e0"),
                HeightRequest = 1,
                Margin = new Thickness(20, 10)
            };
        }

        private Color GetRiskLevelColor(string riskLevel)
        {
            return riskLevel switch
            {
                "Very High" => Color.FromArgb("#b71c1c"),
                "High" => Color.FromArgb("#d32f2f"),
                "Medium" => Color.FromArgb("#f57c00"),
                "Low" => Color.FromArgb("#388e3c"),
                "Very Low" => Color.FromArgb("#2e7d32"),
                _ => Color.FromArgb("#5d6d7e")
            };
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            if (_lastResult == null)
            {
                await DisplayAlert("No Results", "Please analyze an image first", "OK");
                return;
            }

            try
            {
                var report = GenerateDetailedReport(_lastResult);
                var fileName = $"LSB_Detection_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                
                await File.WriteAllTextAsync(filePath, report);

                // On some platforms, you might want to use the Share API
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "LSB Detection Report",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
            }
        }

        private string GenerateDetailedReport(DetectionResult result)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=====================================");
            sb.AppendLine("    LSB STEGANOGRAPHY DETECTION REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine();
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Processing Time: {result.ProcessingTimeMs} ms");
            sb.AppendLine();
            
            sb.AppendLine("OVERALL ASSESSMENT:");
            sb.AppendLine($"  Status: {(result.IsSuspicious ? "STEGANOGRAPHY DETECTED" : "NO STEGANOGRAPHY DETECTED")}");
            sb.AppendLine($"  Risk Level: {result.RiskLevel}");
            sb.AppendLine($"  Confidence: {result.OverallConfidence:P1}");
            sb.AppendLine();
            
            sb.AppendLine("SUMMARY:");
            sb.AppendLine($"  {result.Summary.Replace("\n", "\n  ")}");
            sb.AppendLine();
            
            sb.AppendLine("DETAILED TEST RESULTS:");
            sb.AppendLine("=====================================");
            
            foreach (var test in result.TestResults)
            {
                sb.AppendLine();
                sb.AppendLine($"Test: {test.Value.TestName}");
                sb.AppendLine($"  Status: {(test.Value.IsSuspicious ? "SUSPICIOUS" : "NORMAL")}");
                sb.AppendLine($"  Score: {test.Value.Score:F6}");
                sb.AppendLine($"  Threshold: {test.Value.Threshold:F6}");
                sb.AppendLine($"  Description: {test.Value.Description}");
                sb.AppendLine($"  Interpretation: {test.Value.Interpretation}");
            }
            
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by LSB Steganography Detector v1.0");
            sb.AppendLine("Statistical Analysis Engine");
            
            return sb.ToString();
        }
    }
} 