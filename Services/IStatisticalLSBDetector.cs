using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services
{
    /// <summary>
    /// Interface for statistical LSB steganography detection
    /// </summary>
    public interface IStatisticalLSBDetector
    {
        /// <summary>
        /// Detect LSB steganography in a single image file
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Detection result with all test results and overall assessment</returns>
        Task<DetectionResult> DetectLSBAsync(string imagePath);
    }
} 