using System.Windows.Input;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using LSBSteganographyDetector.Utils;

namespace LSBSteganographyDetector.ViewModels
{
    public class SteganographyToolsViewModel : BaseViewModel
    {
        // Private fields
        private string _sourceImagePath = "";
        private string _stegoImagePath = "";
        private string _extractedMessage = "";
        private string _sourceImageLabel = "No image selected";
        private string _stegoImageLabel = "No image selected";
        private string _message = "";
        private string _messageLength = "0 characters";
        private string _capacity = "(Capacity: unknown)";
        private string _extractedMessageText = "";
        private string _progressText = "";
        private bool _canEmbed = false;
        private bool _canExtract = false;
        private bool _showProgress = false;
        private bool _showExtractedMessage = false;
        private bool _canCopyMessage = false;

        public SteganographyToolsViewModel()
        {
            Title = "Steganography Tools";
            
            // Initialize commands
            SelectSourceImageCommand = new Command(async () => await SelectSourceImageAsync());
            SelectStegoImageCommand = new Command(async () => await SelectStegoImageAsync());
            EmbedMessageCommand = new Command(async () => await EmbedMessageAsync(), () => CanEmbed);
            ExtractMessageCommand = new Command(async () => await ExtractMessageAsync(), () => CanExtract);
            CopyMessageCommand = new Command(async () => await CopyMessageAsync(), () => CanCopyMessage);
        }

        #region Properties

        public string SourceImageLabel
        {
            get => _sourceImageLabel;
            set => SetProperty(ref _sourceImageLabel, value);
        }

        public string StegoImageLabel
        {
            get => _stegoImageLabel;
            set => SetProperty(ref _stegoImageLabel, value);
        }

        public string Message
        {
            get => _message;
            set
            {
                if (SetProperty(ref _message, value))
                {
                    UpdateMessageValidation();
                    UpdateEmbedButtonState();
                }
            }
        }

        public string MessageLength
        {
            get => _messageLength;
            set => SetProperty(ref _messageLength, value);
        }

        public string Capacity
        {
            get => _capacity;
            set => SetProperty(ref _capacity, value);
        }

        public string ExtractedMessageText
        {
            get => _extractedMessageText;
            set => SetProperty(ref _extractedMessageText, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        // Override IsBusy to trigger button state updates
        public new bool IsBusy
        {
            get => base.IsBusy;
            set
            {
                if (base.IsBusy != value)
                {
                    base.IsBusy = value;
                    UpdateEmbedButtonState();
                    
                    // Notify commands to re-evaluate CanExecute
                    ((Command)EmbedMessageCommand).ChangeCanExecute();
                    ((Command)ExtractMessageCommand).ChangeCanExecute();
                    ((Command)CopyMessageCommand).ChangeCanExecute();
                }
            }
        }

        public bool CanEmbed
        {
            get => _canEmbed;
            set => SetProperty(ref _canEmbed, value);
        }

        public bool CanExtract
        {
            get => _canExtract;
            set => SetProperty(ref _canExtract, value);
        }

        public bool ShowProgress
        {
            get => _showProgress;
            set => SetProperty(ref _showProgress, value);
        }

        public bool ShowExtractedMessage
        {
            get => _showExtractedMessage;
            set => SetProperty(ref _showExtractedMessage, value);
        }

        public bool CanCopyMessage
        {
            get => _canCopyMessage;
            set => SetProperty(ref _canCopyMessage, value);
        }

        #endregion

        #region Commands

        public ICommand SelectSourceImageCommand { get; }
        public ICommand SelectStegoImageCommand { get; }
        public ICommand EmbedMessageCommand { get; }
        public ICommand ExtractMessageCommand { get; }
        public ICommand CopyMessageCommand { get; }

        #endregion

        #region Methods

        private async Task SelectSourceImageAsync()
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
                    SourceImageLabel = $"📁 {result.FileName}";
                    
                    // Calculate capacity
                    await UpdateCapacityAsync();
                    UpdateEmbedButtonState();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to select image: {ex.Message}");
            }
        }

        private async Task SelectStegoImageAsync()
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
                    StegoImageLabel = $"📁 {result.FileName}";
                    CanExtract = true;
                    
                    // Hide previous results
                    ShowExtractedMessage = false;
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to select image: {ex.Message}");
            }
        }

        private async Task UpdateCapacityAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_sourceImagePath))
                {
                    var capacity = await LSBTestGenerator.CalculateCapacityAsync(_sourceImagePath);
                    Capacity = $"(Capacity: {capacity} characters)";
                    UpdateMessageValidation();
                }
            }
            catch (Exception)
            {
                Capacity = "(Capacity: Error calculating)";
            }
        }

        private void UpdateMessageValidation()
        {
            var messageLength = Message?.Length ?? 0;
            MessageLength = $"{messageLength} characters";
            
            // Check if message exceeds capacity (simplified logic)
            var capacityText = Capacity;
            
            if (capacityText.Contains("Capacity: ") && capacityText.Contains(" characters"))
            {
                var startIndex = capacityText.IndexOf("Capacity: ") + 10;
                var endIndex = capacityText.IndexOf(" characters");
                if (int.TryParse(capacityText.Substring(startIndex, endIndex - startIndex), out var capacity))
                {
                    // Could add validation logic here for UI feedback
                }
            }
        }

        private void UpdateEmbedButtonState()
        {
            CanEmbed = !IsBusy && 
                      !string.IsNullOrEmpty(_sourceImagePath) && 
                      !string.IsNullOrWhiteSpace(Message);
        }

        private async Task EmbedMessageAsync()
        {
            try
            {
                var message = Message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    await ShowErrorAsync("Error", "Please enter a message to embed");
                    return;
                }

                // Show progress
                IsBusy = true;
                ShowProgress = true;
                ProgressText = "Embedding message...";

                // Choose output location
                var outputPath = Path.Combine(FileSystem.Current.CacheDirectory, 
                    $"stego_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // Embed the message using standard method
                await LSBTestGenerator.EmbedMessageAsync(_sourceImagePath, message, outputPath);

                // Offer choice between saving or sharing
                await OfferSaveOrShareAsync(outputPath, "Steganographic Image Created");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to embed message: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                ShowProgress = false;
                UpdateEmbedButtonState();
            }
        }

        private async Task ExtractMessageAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_stegoImagePath))
                {
                    await ShowErrorAsync("Error", "Please select an image first");
                    return;
                }

                // Show progress
                IsBusy = true;
                ShowProgress = true;
                ProgressText = "Extracting message...";

                // Extract the message using standard method
                _extractedMessage = await LSBTestGenerator.ExtractMessageAsync(_stegoImagePath);

                // Display the result
                if (string.IsNullOrEmpty(_extractedMessage))
                {
                    ExtractedMessageText = "(No hidden message found)";
                    CanCopyMessage = false;
                }
                else
                {
                    ExtractedMessageText = _extractedMessage;
                    CanCopyMessage = true;
                }

                ShowExtractedMessage = true;
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to extract message: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                ShowProgress = false;
            }
        }

        private async Task CopyMessageAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_extractedMessage))
                {
                    await Clipboard.Default.SetTextAsync(_extractedMessage);
                    await ShowSuccessAsync("Success", "Message copied to clipboard!");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to copy message: {ex.Message}");
            }
        }

        private async Task OfferSaveOrShareAsync(string tempFilePath, string title)
        {
            try
            {
                if (Application.Current?.MainPage != null)
                {
                    var action = await Application.Current.MainPage.DisplayActionSheet(
                        "Steganographic Image Created", "Cancel", null, "Save to File", "Share Now");
                    
                    if (action == "Save to File")
                    {
                        await SaveFileAsAsync(tempFilePath, title);
                    }
                    else if (action == "Share Now")
                    {
                        await ShareResultAsync(tempFilePath, title);
                    }
                }
                
                // Always clean up temp file after user action (or cancel)
                try
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch { /* Ignore cleanup errors */ }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to process result: {ex.Message}");
            }
        }

        private async Task SaveFileAsAsync(string sourceFilePath, string title)
        {
            try
            {
                // Get file name suggestion
                var fileName = Path.GetFileName(sourceFilePath);
                
                // Try to use FileSaver from Community Toolkit
                try
                {
                    using var sourceStream = File.OpenRead(sourceFilePath);
                    var result = await FileSaver.Default.SaveAsync(fileName, sourceStream, CancellationToken.None);
                    
                    if (result.IsSuccessful)
                    {
                        await ShowSuccessAsync("Success", $"{title}\n\nFile saved to:\n{result.FilePath}");
                    }
                    else
                    {
                        // Fallback to folder picker if FileSaver fails
                        await SaveFileWithFolderPickerAsync(sourceFilePath, fileName, title);
                    }
                }
                catch (Exception)
                {
                    // Fallback to folder picker if FileSaver isn't available
                    await SaveFileWithFolderPickerAsync(sourceFilePath, fileName, title);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to save file: {ex.Message}");
            }
        }

        private async Task SaveFileWithFolderPickerAsync(string sourceFilePath, string fileName, string title)
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
                    await ShowSuccessAsync("Success", $"{title}\n\nFile saved to:\n{destinationPath}");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error", $"Failed to save file: {ex.Message}");
            }
        }

        private async Task ShareResultAsync(string filePath, string title)
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
                await ShowSuccessAsync("Success", $"{title}\n\nFile saved to: {filePath}");
            }
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }

        private async Task ShowSuccessAsync(string title, string message)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "OK");
            }
        }

        #endregion
    }
} 