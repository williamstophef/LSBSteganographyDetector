using System.Diagnostics;
using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Refactored Geotag Extractor following Single Responsibility Principle
    /// Orchestrates EXIF extraction, location processing, and file handling
    /// </summary>
    public class GeotagExtractorRefactored : IGeotagExtractor
    {
        private readonly IExifDataExtractor _exifExtractor;
        private readonly ILocationProcessor _locationProcessor;
        private readonly IImageFileHandler _fileHandler;

        public GeotagExtractorRefactored(
            IExifDataExtractor exifExtractor,
            ILocationProcessor locationProcessor,
            IImageFileHandler fileHandler)
        {
            _exifExtractor = exifExtractor ?? throw new ArgumentNullException(nameof(exifExtractor));
            _locationProcessor = locationProcessor ?? throw new ArgumentNullException(nameof(locationProcessor));
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
        }

        /// <summary>
        /// Constructor with default implementations
        /// </summary>
        public GeotagExtractorRefactored() : this(
            new ExifDataExtractor(),
            new LocationProcessor(),
            new ImageFileHandler())
        {
        }

        public async Task<GeotagAnalysisResult> ExtractGeotagDataAsync(string imagePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GeotagAnalysisResult
            {
                StartTime = DateTime.Now,
                SourcePath = imagePath,
                IsBatchAnalysis = false,
                TotalImages = 1
            };

            try
            {
                var locationData = await ExtractLocationDataFromImageAsync(imagePath);
                if (locationData != null)
                {
                    result.ImageLocations.Add(locationData);
                    result.GeotaggedImages = 1;
                    result.UniqueLocations = 1;
                }

                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                // Return partial results on error
                return result;
            }
        }

        private async Task<ImageLocationData?> ExtractLocationDataFromImageAsync(string imagePath)
        {
            try
            {
                using var image = await _fileHandler.LoadImageAsync(imagePath);
                var fileInfo = _fileHandler.GetFileInfo(imagePath);
                
                // Extract GPS data from EXIF
                var gpsData = _exifExtractor.ExtractGpsData(image);
                
                if (gpsData == null || !gpsData.HasGpsData)
                {
                    return null; // No GPS data found
                }

                // Extract camera information
                var cameraInfo = _exifExtractor.ExtractCameraInfo(image);
                
                // Extract timestamp
                var timestamp = _exifExtractor.ExtractTimestamp(image) ?? fileInfo.CreationTime;

                var locationData = new ImageLocationData
                {
                    FileName = Path.GetFileName(imagePath),
                    FilePath = imagePath,
                    Latitude = gpsData.Latitude!.Value,
                    Longitude = gpsData.Longitude!.Value,
                    Altitude = gpsData.Altitude,
                    Timestamp = timestamp,
                    FileSizeBytes = fileInfo.Length,
                    CameraModel = cameraInfo.CameraModel,
                    CameraMake = cameraInfo.CameraMake,
                    Accuracy = gpsData.Accuracy,
                    GpsProcessingMethod = gpsData.ProcessingMethod,
                    GpsTimestamp = gpsData.GpsTimestamp
                };

                // Generate location description using location processor
                locationData.LocationDescription = _locationProcessor.GenerateLocationDescription(
                    locationData.Latitude, locationData.Longitude);

                return locationData;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
} 