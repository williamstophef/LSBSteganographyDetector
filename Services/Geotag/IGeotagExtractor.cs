using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Interface for extracting geotag data from images
    /// </summary>
    public interface IGeotagExtractor
    {
        /// <summary>
        /// Extract geotag data from a single image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Geotag analysis result for single image</returns>
        Task<GeotagAnalysisResult> ExtractGeotagDataAsync(string imagePath);
    }
} 