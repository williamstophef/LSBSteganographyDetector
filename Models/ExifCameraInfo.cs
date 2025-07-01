namespace LSBSteganographyDetector.Models
{
    /// <summary>
    /// Camera information extracted from EXIF metadata
    /// </summary>
    public class ExifCameraInfo
    {
        public string? CameraMake { get; set; }
        public string? CameraModel { get; set; }
        public string? Software { get; set; }
        public double? FocalLength { get; set; }
        public string? IsoSpeedRating { get; set; }
        public string? ExposureTime { get; set; }
        public string? FNumber { get; set; }
        public string? Flash { get; set; }
        public int? Orientation { get; set; }
    }
} 