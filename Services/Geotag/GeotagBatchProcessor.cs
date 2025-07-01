using System.Diagnostics;
using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Processes multiple images for geotag extraction in batch mode
    /// </summary>
    public class GeotagBatchProcessor : IGeotagBatchProcessor
    {
        private readonly IGeotagExtractor _geotagExtractor;
        private readonly IImageFileHandler _fileHandler;
        private readonly ILocationProcessor _locationProcessor;

        public GeotagBatchProcessor(
            IGeotagExtractor geotagExtractor,
            IImageFileHandler fileHandler,
            ILocationProcessor locationProcessor)
        {
            _geotagExtractor = geotagExtractor ?? throw new ArgumentNullException(nameof(geotagExtractor));
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
            _locationProcessor = locationProcessor ?? throw new ArgumentNullException(nameof(locationProcessor));
        }

        public async Task<GeotagAnalysisResult> ExtractBatchGeotagDataAsync(string folderPath, IProgress<GeotagExtractionProgress>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GeotagAnalysisResult
            {
                StartTime = DateTime.Now,
                SourcePath = folderPath,
                IsBatchAnalysis = true
            };

            try
            {
                // Get all supported image files
                var imageFiles = _fileHandler.GetSupportedFiles(folderPath);
                result.TotalImages = imageFiles.Length;

                if (imageFiles.Length == 0)
                {
                    result.EndTime = DateTime.Now;
                    result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return result;
                }

                // Process each image
                for (int i = 0; i < imageFiles.Length; i++)
                {
                    var imagePath = imageFiles[i];
                    
                    try
                    {
                        // Report progress
                        progress?.Report(new GeotagExtractionProgress
                        {
                            CurrentFile = Path.GetFileName(imagePath),
                            ProcessedCount = i,
                            TotalCount = imageFiles.Length,
                            GeotaggedCount = result.GeotaggedImages
                        });

                        // Extract geotag data from single image
                        var singleResult = await _geotagExtractor.ExtractGeotagDataAsync(imagePath);
                        
                        // Add any found location data to batch result
                        if (singleResult.ImageLocations.Any())
                        {
                            result.ImageLocations.AddRange(singleResult.ImageLocations);
                            result.GeotaggedImages++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue processing on error
                        progress?.Report(new GeotagExtractionProgress
                        {
                            CurrentFile = Path.GetFileName(imagePath),
                            ProcessedCount = i,
                            TotalCount = imageFiles.Length,
                            GeotaggedCount = result.GeotaggedImages,
                            ErrorMessage = $"Failed to process: {ex.Message}"
                        });
                    }
                }

                // Calculate unique locations using location processor
                result.UniqueLocations = _locationProcessor.CalculateUniqueLocations(result.ImageLocations);

                // Final progress report
                progress?.Report(new GeotagExtractionProgress
                {
                    CurrentFile = "Analysis complete",
                    ProcessedCount = imageFiles.Length,
                    TotalCount = imageFiles.Length,
                    GeotaggedCount = result.GeotaggedImages
                });

                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                throw new InvalidOperationException($"Batch GPS extraction failed: {ex.Message}", ex);
            }
        }
    }
} 