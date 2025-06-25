using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using LSBSteganographyDetector.Utils;

namespace LSBSteganographyDetector.Views
{
    public partial class SteganographyToolsPage : ContentPage
    {
        private string _sourceImagePath = "";
        private string _stegoImagePath = "";
        private string _testImagePath = "";
        private string _extractedMessage = "";

        public SteganographyToolsPage()
        {
            InitializeComponent();
        }

        #region Source Image and Embed Message

        private async void OnSelectSourceImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Source Image",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff" } }
                    })
                });

                if (result != null)
                {
                    _sourceImagePath = result.FullPath;
                    SourceImageLabel.Text = $"📁 {result.FileName}";
                    
                    // Calculate capacity
                    await UpdateCapacity();
                    UpdateEmbedButtonState();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
            }
        }

        private async Task UpdateCapacity()
        {
            try
            {
                if (!string.IsNullOrEmpty(_sourceImagePath))
                {
                    var capacity = await LSBTestGenerator.CalculateCapacityAsync(_sourceImagePath);
                    CapacityLabel.Text = $"(Capacity: {capacity} characters)";
                    UpdateMessageValidation();
                }
            }
            catch (Exception)
            {
                CapacityLabel.Text = "(Capacity: Error calculating)";
            }
        }

        private void OnMessageTextChanged(object sender, TextChangedEventArgs e)
        {
            var length = e.NewTextValue?.Length ?? 0;
            MessageLengthLabel.Text = $"{length} characters";
            UpdateMessageValidation();
            UpdateEmbedButtonState();
        }

        private void UpdateMessageValidation()
        {
            var messageLength = MessageEditor.Text?.Length ?? 0;
            var capacityText = CapacityLabel.Text;
            
            if (capacityText.Contains("Capacity: ") && capacityText.Contains(" characters"))
            {
                var startIndex = capacityText.IndexOf("Capacity: ") + 10;
                var endIndex = capacityText.IndexOf(" characters");
                if (int.TryParse(capacityText.Substring(startIndex, endIndex - startIndex), out var capacity))
                {
                    if (messageLength > capacity)
                    {
                        MessageLengthLabel.TextColor = Color.FromArgb("#e74c3c");
                        CapacityLabel.TextColor = Color.FromArgb("#e74c3c");
                    }
                    else
                    {
                        MessageLengthLabel.TextColor = Color.FromArgb("#7f8c8d");
                        CapacityLabel.TextColor = Color.FromArgb("#7f8c8d");
                    }
                }
            }
        }

        private void UpdateEmbedButtonState()
        {
            var canEmbed = !string.IsNullOrEmpty(_sourceImagePath) && 
                          !string.IsNullOrWhiteSpace(MessageEditor.Text);
            EmbedStandardButton.IsEnabled = canEmbed;
            EmbedPythonButton.IsEnabled = canEmbed;
        }

        private async void OnEmbedStandardClicked(object sender, EventArgs e)
        {
            await EmbedMessage(false, "Standard");
        }

        private async void OnEmbedPythonClicked(object sender, EventArgs e)
        {
            await EmbedMessage(true, "Python Compatible");
        }

        private async Task EmbedMessage(bool usePythonCompatible, string methodName)
        {
            try
            {
                var message = MessageEditor.Text;
                if (string.IsNullOrWhiteSpace(message))
                {
                    await DisplayAlert("Error", "Please enter a message to embed", "OK");
                    return;
                }

                // Show progress
                ShowProgress($"Embedding message ({methodName})...");

                // Choose output location
                var suffix = usePythonCompatible ? "python" : "standard";
                var outputPath = Path.Combine(FileSystem.Current.CacheDirectory, 
                    $"stego_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // Embed the message using the appropriate method
                if (usePythonCompatible)
                {
                    await LSBTestGenerator.EmbedMessagePythonCompatibleAsync(_sourceImagePath, message, outputPath);
                }
                else
                {
                    await LSBTestGenerator.EmbedMessageAsync(_sourceImagePath, message, outputPath);
                }

                // Hide progress
                HideProgress();

                // Offer choice between saving or sharing
                await OfferSaveOrShare(outputPath, $"Steganographic Image Created ({methodName})");
            }
            catch (Exception ex)
            {
                HideProgress();
                await DisplayAlert("Error", $"Failed to embed message using {methodName}: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Extract Message

        private async void OnSelectStegoImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Steganographic Image",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff" } }
                    })
                });

                if (result != null)
                {
                    _stegoImagePath = result.FullPath;
                    StegoImageLabel.Text = $"📁 {result.FileName}";
                    ExtractStandardButton.IsEnabled = true;
                    ExtractPythonButton.IsEnabled = true;
                    
                    // Hide previous results
                    ExtractedMessagePanel.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
            }
        }

        private async void OnExtractStandardClicked(object sender, EventArgs e)
        {
            await ExtractMessage(false, "Standard");
        }

        private async void OnExtractPythonClicked(object sender, EventArgs e)
        {
            await ExtractMessage(true, "Python Compatible");
        }

        private async Task ExtractMessage(bool usePythonCompatible, string methodName)
        {
            try
            {
                if (string.IsNullOrEmpty(_stegoImagePath))
                {
                    await DisplayAlert("Error", "Please select an image first", "OK");
                    return;
                }

                // Show progress
                ShowProgress($"Extracting message ({methodName})...");

                // Extract the message using the appropriate method
                if (usePythonCompatible)
                {
                    _extractedMessage = await LSBTestGenerator.ExtractMessagePythonCompatibleAsync(_stegoImagePath);
                }
                else
                {
                    _extractedMessage = await LSBTestGenerator.ExtractMessageAsync(_stegoImagePath);
                }

                // Hide progress
                HideProgress();

                // Display the result
                if (string.IsNullOrEmpty(_extractedMessage))
                {
                    ExtractedMessageLabel.Text = $"(No hidden message found using {methodName} method)";
                    ExtractedMessageLabel.TextColor = Color.FromArgb("#95a5a6");
                    CopyMessageButton.IsEnabled = false;
                }
                else
                {
                    ExtractedMessageLabel.Text = $"[{methodName}] {_extractedMessage}";
                    ExtractedMessageLabel.TextColor = Color.FromArgb("#2c3e50");
                    CopyMessageButton.IsEnabled = true;
                }

                ExtractedMessagePanel.IsVisible = true;
            }
            catch (Exception ex)
            {
                HideProgress();
                await DisplayAlert("Error", $"Failed to extract message using {methodName}: {ex.Message}", "OK");
            }
        }

        private async void OnCopyMessageClicked(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_extractedMessage))
                {
                    await Clipboard.Default.SetTextAsync(_extractedMessage);
                    await DisplayAlert("Success", "Message copied to clipboard!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to copy message: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Test Suite Generation

        private async void OnSelectTestImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Base Image for Test Suite",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff" } }
                    })
                });

                if (result != null)
                {
                    _testImagePath = result.FullPath;
                    TestImageLabel.Text = $"📁 {result.FileName}";
                    GenerateTestSuiteButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
            }
        }

        private async void OnGenerateTestSuiteClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_testImagePath))
                {
                    await DisplayAlert("Error", "Please select a base image first", "OK");
                    return;
                }

                // Select output folder
                var folderResult = await FolderPicker.Default.PickAsync(CancellationToken.None);
                if (folderResult == null || !folderResult.IsSuccessful)
                {
                    return;
                }

                var outputDir = Path.Combine(folderResult.Folder.Path, $"LSB_TestSuite_{DateTime.Now:yyyyMMdd_HHmmss}");

                // Show progress
                ShowProgress("Generating test suite...");

                // Generate the test suite
                await LSBTestGenerator.CreateTestSuiteAsync(_testImagePath, outputDir);

                // Hide progress
                HideProgress();

                // Show success message
                var message = $"Test suite generated successfully!\n\nLocation: {outputDir}\n\n" +
                             "The suite contains 7 test images:\n" +
                             "• Clean control image\n" +
                             "• Short, medium, and long messages\n" +
                             "• Random noise at different levels\n" +
                             "• Binary data embedding\n\n" +
                             "You can now use these images to test the detector.";

                await DisplayAlert("Test Suite Generated", message, "OK");
            }
            catch (Exception ex)
            {
                HideProgress();
                await DisplayAlert("Error", $"Failed to generate test suite: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Helper Methods

        private void ShowProgress(string message)
        {
            ProgressLabel.Text = message;
            ProgressIndicator.IsRunning = true;
            ProgressPanel.IsVisible = true;
            
            // Disable buttons during processing
            EmbedStandardButton.IsEnabled = false;
            EmbedPythonButton.IsEnabled = false;
            ExtractStandardButton.IsEnabled = false;
            ExtractPythonButton.IsEnabled = false;
            GenerateTestSuiteButton.IsEnabled = false;
        }

        private void HideProgress()
        {
            ProgressIndicator.IsRunning = false;
            ProgressPanel.IsVisible = false;
            
            // Re-enable buttons
            UpdateEmbedButtonState();
            ExtractStandardButton.IsEnabled = !string.IsNullOrEmpty(_stegoImagePath);
            ExtractPythonButton.IsEnabled = !string.IsNullOrEmpty(_stegoImagePath);
            GenerateTestSuiteButton.IsEnabled = !string.IsNullOrEmpty(_testImagePath);
        }

        private async Task OfferSaveOrShare(string tempFilePath, string title)
        {
            var action = await DisplayActionSheet("Steganographic Image Created", "Cancel", null, "Save to File", "Share Now");
            
            if (action == "Save to File")
            {
                await SaveFileAs(tempFilePath, title);
            }
            else if (action == "Share Now")
            {
                await ShareResult(tempFilePath, title);
            }
            
            // Always clean up temp file after user action (or cancel)
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch { /* Ignore cleanup errors */ }
        }

        private async Task SaveFileAs(string sourceFilePath, string title)
        {
            try
            {
                // Get file name suggestion
                var fileName = Path.GetFileName(sourceFilePath);
                var fileExtension = Path.GetExtension(sourceFilePath);
                var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
                
                // Try to use FileSaver from Community Toolkit
                try
                {
                    using var sourceStream = File.OpenRead(sourceFilePath);
                    var result = await FileSaver.Default.SaveAsync(fileName, sourceStream, CancellationToken.None);
                    
                    if (result.IsSuccessful)
                    {
                        await DisplayAlert("Success", $"{title}\n\nFile saved to:\n{result.FilePath}", "OK");
                    }
                    else
                    {
                        // Fallback to folder picker if FileSaver fails
                        await SaveFileWithFolderPicker(sourceFilePath, fileName, title);
                    }
                }
                catch (Exception)
                {
                    // Fallback to folder picker if FileSaver isn't available
                    await SaveFileWithFolderPicker(sourceFilePath, fileName, title);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
            }
        }

        private async Task SaveFileWithFolderPicker(string sourceFilePath, string fileName, string title)
        {
            try
            {
                var folderResult = await FolderPicker.Default.PickAsync(CancellationToken.None);
                if (folderResult != null && folderResult.IsSuccessful)
                {
                    var destinationPath = Path.Combine(folderResult.Folder.Path, fileName);
                    
                    // Handle duplicate files
                    var counter = 1;
                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);
                    
                    while (File.Exists(destinationPath))
                    {
                        var newFileName = $"{baseName}_{counter}{extension}";
                        destinationPath = Path.Combine(folderResult.Folder.Path, newFileName);
                        counter++;
                    }
                    
                    File.Copy(sourceFilePath, destinationPath);
                    await DisplayAlert("Success", $"{title}\n\nFile saved to:\n{destinationPath}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
            }
        }

        private async Task ShareResult(string filePath, string title)
        {
            try
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = title,
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception)
            {
                // Fallback: just show success message
                await DisplayAlert("Success", $"{title}\n\nFile saved to: {filePath}", "OK");
            }
        }

        #endregion
    }
} 