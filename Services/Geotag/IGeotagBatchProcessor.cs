using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Interface for batch processing geotag extraction from multiple images
    /// </summary>
    public interface IGeotagBatchProcessor
    {
        /// <summary>
        /// Extract geotag data from all images in a folder
        /// </summary>
        /// <param name="folderPath">Path to folder containing images</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <returns>Batch geotag analysis result</returns>
        Task<GeotagAnalysisResult> ExtractBatchGeotagDataAsync(string folderPath, IProgress<GeotagExtractionProgress>? progress = null);
    }
} 