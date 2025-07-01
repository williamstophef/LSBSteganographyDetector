using System.Diagnostics;
using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.BatchProcessing
{
    /// <summary>
    /// Batch processor for processing entire folders of images
    /// </summary>
    public class FolderBatchProcessor : IBatchProcessor
    {
        private readonly IStatisticalLSBDetector _detector;
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

        public IReadOnlyList<string> SupportedExtensions => _supportedExtensions;

        public FolderBatchProcessor(IStatisticalLSBDetector detector)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        }

        public async Task<BatchProcessingSummary> ProcessFolderAsync(string folderPath, IProgress<BatchProcessingProgress>? progress = null)
        {
            var summary = new BatchProcessingSummary
            {
                StartTime = DateTime.Now
            };

            try
            {
                // Debug: Report starting batch processing
                progress?.Report(new BatchProcessingProgress
                {
                    CurrentFile = "Scanning folder...",
                    ProcessedCount = 0,
                    TotalCount = 0,
                    HighRiskCount = 0
                });

                // Get all image files in the folder
                var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();

                summary.TotalImages = imageFiles.Length;

                // Debug: Report file count
                progress?.Report(new BatchProcessingProgress
                {
                    CurrentFile = $"Found {imageFiles.Length} images",
                    ProcessedCount = 0,
                    TotalCount = imageFiles.Length,
                    HighRiskCount = 0
                });

                if (imageFiles.Length == 0)
                {
                    summary.EndTime = DateTime.Now;
                    return summary;
                }

                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < imageFiles.Length; i++)
                {
                    var imagePath = imageFiles[i];
                    
                    try
                    {
                        var result = await _detector.DetectLSBAsync(imagePath);
                        var fileInfo = new FileInfo(imagePath);

                        var batchResult = new BatchDetectionResult
                        {
                            ImagePath = imagePath,
                            FileName = Path.GetFileName(imagePath),
                            Result = result,
                            FileSizeBytes = fileInfo.Length,
                            ProcessedAt = DateTime.Now
                        };

                        summary.AllResults.Add(batchResult);

                        // Categorize by risk level
                        CategorizeResult(summary, batchResult);

                        summary.ProcessedImages++;

                        // Debug: Log the categorization
                        progress?.Report(new BatchProcessingProgress
                        {
                            CurrentFile = $"Categorized {batchResult.FileName} as {result.RiskLevel}",
                            ProcessedCount = summary.ProcessedImages,
                            TotalCount = summary.TotalImages,
                            HighRiskCount = summary.HighRiskImages,
                            CurrentFileRisk = result.RiskLevel
                        });

                        // Report progress
                        progress?.Report(new BatchProcessingProgress
                        {
                            CurrentFile = batchResult.FileName,
                            ProcessedCount = summary.ProcessedImages,
                            TotalCount = summary.TotalImages,
                            HighRiskCount = summary.HighRiskImages,
                            CurrentFileRisk = result.RiskLevel
                        });
                    }
                    catch (Exception ex)
                    {
                        // Continue processing on error
                        progress?.Report(new BatchProcessingProgress
                        {
                            CurrentFile = Path.GetFileName(imagePath),
                            ProcessedCount = summary.ProcessedImages,
                            TotalCount = summary.TotalImages,
                            HighRiskCount = summary.HighRiskImages,
                            CurrentFileRisk = "Error",
                            ErrorMessage = $"Failed to process: {ex.Message}"
                        });
                    }
                }

                stopwatch.Stop();
                summary.TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                summary.EndTime = DateTime.Now;

                return summary;
            }
            catch (Exception ex)
            {
                summary.EndTime = DateTime.Now;
                throw new InvalidOperationException($"Batch processing failed: {ex.Message}", ex);
            }
        }

        private void CategorizeResult(BatchProcessingSummary summary, BatchDetectionResult batchResult)
        {
            switch (batchResult.Result.RiskLevel)
            {
                case "Very High":
                case "High":
                    summary.HighRiskImages++;
                    summary.HighRiskResults.Add(batchResult);
                    break;
                case "Medium":
                    summary.MediumRiskImages++;
                    summary.MediumRiskResults.Add(batchResult);
                    break;
                default:
                    summary.LowRiskImages++;
                    summary.LowRiskResults.Add(batchResult);
                    break;
            }
        }
    }
} 