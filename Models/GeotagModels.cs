using System;
using System.Collections.Generic;

namespace LSBSteganographyDetector.Models
{
    public class ImageLocationData
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Altitude { get; set; }
        public DateTime Timestamp { get; set; }
        public long FileSizeBytes { get; set; }
        public string? LocationDescription { get; set; }
        public string? CameraModel { get; set; }
        public string? CameraMake { get; set; }
        
        // Additional EXIF data
        public double? Accuracy { get; set; }
        public string? GpsProcessingMethod { get; set; }
        public DateTime? GpsTimestamp { get; set; }
    }

    public class GeotagAnalysisResult
    {
        public int TotalImages { get; set; }
        public int GeotaggedImages { get; set; }
        public int UniqueLocations { get; set; }
        public List<ImageLocationData> ImageLocations { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long ProcessingTimeMs { get; set; }
        public string SourcePath { get; set; } = "";
        public bool IsBatchAnalysis { get; set; }
        
        // Statistics
        public double GeotagPercentage => TotalImages > 0 ? (double)GeotaggedImages / TotalImages * 100 : 0;
        public TimeSpan ProcessingDuration => EndTime - StartTime;
    }

    public class GeotagExtractionProgress
    {
        public string CurrentFile { get; set; } = "";
        public int ProcessedCount { get; set; }
        public int TotalCount { get; set; }
        public int GeotaggedCount { get; set; }
        public string? ErrorMessage { get; set; }
        public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;
    }

    public class LocationCluster
    {
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public List<ImageLocationData> Images { get; set; } = new();
        public string? LocationName { get; set; }
        public double RadiusMeters { get; set; }
    }

    public class ExifGpsData
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Altitude { get; set; }
        public DateTime? GpsTimestamp { get; set; }
        public string? ProcessingMethod { get; set; }
        public string? MapDatum { get; set; }
        public double? Accuracy { get; set; }

        public bool HasGpsData => 
            Latitude.HasValue && 
            Longitude.HasValue && 
            Latitude.Value != 0.0 && 
            Longitude.Value != 0.0 &&
            Math.Abs(Latitude.Value) <= 90 && 
            Math.Abs(Longitude.Value) <= 180;
    }
} 