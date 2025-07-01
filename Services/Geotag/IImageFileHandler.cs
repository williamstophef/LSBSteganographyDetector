using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Interface for handling image file operations
    /// </summary>
    public interface IImageFileHandler
    {
        /// <summary>
        /// Supported image file extensions
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }
        
        /// <summary>
        /// Load an image from file path
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Loaded image</returns>
        Task<SLImage> LoadImageAsync(string imagePath);
        
        /// <summary>
        /// Get basic file information
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>File information</returns>
        FileInfo GetFileInfo(string imagePath);
        
        /// <summary>
        /// Check if file extension is supported
        /// </summary>
        /// <param name="filePath">Path to check</param>
        /// <returns>True if supported</returns>
        bool IsSupported(string filePath);
        
        /// <summary>
        /// Get all supported image files in a directory
        /// </summary>
        /// <param name="directoryPath">Directory to scan</param>
        /// <returns>Array of supported image file paths</returns>
        string[] GetSupportedFiles(string directoryPath);
    }
} 