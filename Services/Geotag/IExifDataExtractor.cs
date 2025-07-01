using LSBSteganographyDetector.Models;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Interface for extracting EXIF metadata from images
    /// </summary>
    public interface IExifDataExtractor
    {
        /// <summary>
        /// Extract GPS data from image EXIF metadata
        /// </summary>
        /// <param name="image">Image to extract GPS data from</param>
        /// <returns>GPS data if available, null otherwise</returns>
        ExifGpsData? ExtractGpsData(SLImage image);
        
        /// <summary>
        /// Extract camera information from EXIF metadata
        /// </summary>
        /// <param name="image">Image to extract camera info from</param>
        /// <returns>Camera information</returns>
        ExifCameraInfo ExtractCameraInfo(SLImage image);
        
        /// <summary>
        /// Extract timestamp from EXIF metadata
        /// </summary>
        /// <param name="image">Image to extract timestamp from</param>
        /// <returns>Image timestamp if available</returns>
        DateTime? ExtractTimestamp(SLImage image);
    }
} 