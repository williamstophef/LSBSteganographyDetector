using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Handles image file operations and validation
    /// </summary>
    public class ImageFileHandler : IImageFileHandler
    {
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

        public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

        public async Task<SLImage> LoadImageAsync(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            if (!IsSupported(imagePath))
                throw new NotSupportedException($"Unsupported image format: {Path.GetExtension(imagePath)}");

            try
            {
                return await SLImage.LoadAsync(imagePath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to load image: {ex.Message}", ex);
            }
        }

        public FileInfo GetFileInfo(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"File not found: {imagePath}");

            return new FileInfo(imagePath);
        }

        public bool IsSupported(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return _supportedExtensions.Contains(extension);
        }

        public string[] GetSupportedFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            return Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(file => IsSupported(file))
                .ToArray();
        }
    }
} 