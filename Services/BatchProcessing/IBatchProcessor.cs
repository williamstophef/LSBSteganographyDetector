using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.BatchProcessing
{
    /// <summary>
    /// Interface for batch processing of image files for steganography detection
    /// </summary>
    public interface IBatchProcessor
    {
        /// <summary>
        /// Process all images in a folder for LSB steganography detection
        /// </summary>
        /// <param name="folderPath">Path to folder containing images</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <returns>Batch processing summary with all results</returns>
        Task<BatchProcessingSummary> ProcessFolderAsync(string folderPath, IProgress<BatchProcessingProgress>? progress = null);
        
        /// <summary>
        /// Supported image file extensions
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }
    }
} 