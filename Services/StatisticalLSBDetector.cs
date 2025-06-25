using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services
{
    public class StatisticalLSBDetector
    {
        // Conservative thresholds - significantly reduce false positives while catching real steganography
        private const double CHI_SQUARE_THRESHOLD = 9.0; // Much higher threshold (was 7.0)
        private const double SAMPLE_PAIR_THRESHOLD = 0.25; // More tolerance for natural variation (was 0.25)
        private const double RS_THRESHOLD = 0.02; // Higher threshold for block patterns (was 0.08)
        private const double ENTROPY_THRESHOLD = 0.997; // High entropy but more sensitive to real steganography (was 0.999)
        private const double HISTOGRAM_THRESHOLD = 0.1; // Much higher histogram threshold (was 0.08)
        private const double PYTHON_LSB_THRESHOLD = 0.30; // Higher Python pattern threshold (was 0.12)

        // Supported image extensions
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif" };

        public async Task<DetectionResult> DetectLSBAsync(string imagePath)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
                var fileInfo = new FileInfo(imagePath);
                
                var result = new DetectionResult();
                
                // Run all statistical tests
                result.TestResults["ChiSquare"] = await Task.Run(() => ChiSquareTest(image));
                result.TestResults["SamplePair"] = await Task.Run(() => SamplePairAnalysis(image));
                result.TestResults["RSAnalysis"] = await Task.Run(() => RSAnalysis(image));
                result.TestResults["Entropy"] = await Task.Run(() => EntropyAnalysis(image));
                result.TestResults["Histogram"] = await Task.Run(() => HistogramAnalysis(image));
                result.TestResults["PythonLSB"] = await Task.Run(() => PythonLSBTest(image));
                
                // Calculate overall assessment
                CalculateOverallResult(result, fileInfo);
                
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new DetectionResult
                {
                    IsSuspicious = false,
                    OverallConfidence = 0,
                    Summary = $"Error analyzing image: {ex.Message}",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        public async Task<BatchProcessingSummary> DetectLSBBatchAsync(string folderPath, IProgress<BatchProcessingProgress>? progress = null)
        {
            var summary = new BatchProcessingSummary
            {
                StartTime = DateTime.Now
            };

            try
            {
                // Get all image files in the folder
                var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();

                summary.TotalImages = imageFiles.Length;

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
                        var result = await DetectLSBAsync(imagePath);
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
                        switch (result.RiskLevel)
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

                        summary.ProcessedImages++;

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
                        // Log error but continue processing other files
                        System.Diagnostics.Debug.WriteLine($"Error processing {imagePath}: {ex.Message}");
                        
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

        private TestResult ChiSquareTest(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // Chi-square test for LSB randomness
            var lsbCounts = new int[2]; // 0s and 1s in LSB
            var totalSamples = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        
                        // Check LSB of each color channel
                        lsbCounts[pixel.R & 1]++;
                        lsbCounts[pixel.G & 1]++;
                        lsbCounts[pixel.B & 1]++;
                        totalSamples += 3;
                    }
                }
            });

            // Expected frequency (should be roughly equal for natural images)
            double expected = totalSamples / 2.0;
            
            // Calculate chi-square statistic
            double chiSquare = 0;
            foreach (var count in lsbCounts)
            {
                double diff = count - expected;
                chiSquare += (diff * diff) / expected;
            }

            bool isSuspicious = chiSquare > CHI_SQUARE_THRESHOLD;

            return new TestResult
            {
                TestName = "Chi-Square Test",
                Score = chiSquare,
                Threshold = CHI_SQUARE_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Tests if LSB distribution deviates from expected randomness",
                Interpretation = isSuspicious 
                    ? "LSB distribution is significantly non-random, suggesting steganography"
                    : "LSB distribution appears random and natural"
            };
        }

        private TestResult SamplePairAnalysis(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // Sample Pair Analysis - checks for correlations between adjacent pixels
            var pairs = new List<(int, int)>();
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length - 1; x++)
                    {
                        var pixel1 = row[x];
                        var pixel2 = row[x + 1];
                        
                        // Compare LSBs of adjacent red pixels
                        pairs.Add((pixel1.R & 1, pixel2.R & 1));
                    }
                }
            });

            // Calculate sample pair statistics
            var sameCount = pairs.Count(p => p.Item1 == p.Item2);
            var totalPairs = pairs.Count;
            var samePairRatio = (double)sameCount / totalPairs;
            
            // For natural images, should be around 0.5
            // Steganography often creates correlation
            var deviation = Math.Abs(samePairRatio - 0.5);
            bool isSuspicious = deviation > SAMPLE_PAIR_THRESHOLD;

            return new TestResult
            {
                TestName = "Sample Pair Analysis",
                Score = deviation,
                Threshold = SAMPLE_PAIR_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Analyzes correlation between adjacent pixel LSBs",
                Interpretation = isSuspicious
                    ? $"Unusual correlation in adjacent pixels (ratio: {samePairRatio:F3})"
                    : $"Normal correlation in adjacent pixels (ratio: {samePairRatio:F3})"
            };
        }

        private TestResult RSAnalysis(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // RS (Regular/Singular) Analysis
            // Simplified version - analyzes block smoothness
            
            var regularBlocks = 0;
            var singularBlocks = 0;
            var totalBlocks = 0;
            const int blockSize = 4;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height - blockSize; y += blockSize)
                {
                    for (int x = 0; x < accessor.Width - blockSize; x += blockSize)
                    {
                        var block = new List<byte>();
                        
                        // Extract block
                        for (int by = 0; by < blockSize; by++)
                        {
                            var row = accessor.GetRowSpan(y + by);
                            for (int bx = 0; bx < blockSize; bx++)
                            {
                                block.Add(row[x + bx].R);
                            }
                        }
                        
                        // Calculate block characteristics
                        var variance = CalculateVariance(block);
                        
                        if (variance < 10) // Low variance = regular
                            regularBlocks++;
                        else if (variance > 100) // High variance = singular
                            singularBlocks++;
                            
                        totalBlocks++;
                    }
                }
            });

            var rsRatio = totalBlocks > 0 ? (double)(regularBlocks - singularBlocks) / totalBlocks : 0;
            var deviation = Math.Abs(rsRatio);
            bool isSuspicious = deviation > RS_THRESHOLD;

            return new TestResult
            {
                TestName = "RS Analysis",
                Score = deviation,
                Threshold = RS_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Analyzes regular vs singular block patterns",
                Interpretation = isSuspicious
                    ? "Unusual block pattern distribution detected"
                    : "Normal block pattern distribution"
            };
        }

        private TestResult EntropyAnalysis(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // Calculate entropy of LSB plane
            var lsbValues = new List<byte>();
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        lsbValues.Add((byte)(pixel.R & 1));
                        lsbValues.Add((byte)(pixel.G & 1));
                        lsbValues.Add((byte)(pixel.B & 1));
                    }
                }
            });

            var entropy = CalculateEntropy(lsbValues);
            bool isSuspicious = entropy > ENTROPY_THRESHOLD;

            return new TestResult
            {
                TestName = "Entropy Analysis",
                Score = entropy,
                Threshold = ENTROPY_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Measures randomness in LSB plane",
                Interpretation = isSuspicious
                    ? "LSB plane has unusually high entropy (too random)"
                    : "LSB plane entropy is within normal range"
            };
        }

        private TestResult HistogramAnalysis(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // Analyze histogram of pixel values for anomalies
            var histogram = new int[256];
            var totalPixels = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        histogram[pixel.R]++;
                        histogram[pixel.G]++;
                        histogram[pixel.B]++;
                        totalPixels += 3;
                    }
                }
            });

            // Look for unusual patterns in even/odd value pairs
            var suspiciousPatterns = 0;
            for (int i = 0; i < 255; i += 2)
            {
                var evenCount = histogram[i];
                var oddCount = histogram[i + 1];
                var total = evenCount + oddCount;
                
                if (total > 0)
                {
                    var ratio = Math.Abs((double)evenCount / total - 0.5);
                    if (ratio > 0.3) // Significant deviation
                        suspiciousPatterns++;
                }
            }

            var suspiciousRatio = (double)suspiciousPatterns / 128;
            bool isSuspicious = suspiciousRatio > HISTOGRAM_THRESHOLD;

            return new TestResult
            {
                TestName = "Histogram Analysis",
                Score = suspiciousRatio,
                Threshold = HISTOGRAM_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Analyzes pixel value distribution for anomalies",
                Interpretation = isSuspicious
                    ? "Unusual patterns in pixel value distribution"
                    : "Normal pixel value distribution"
            };
        }

        private TestResult PythonLSBTest(SixLabors.ImageSharp.Image<Rgb24> image)
        {
            // Specific test for Python-style LSB embedding (all channels, repeating pattern)
            var lsbSequenceR = new List<bool>();
            var lsbSequenceG = new List<bool>();
            var lsbSequenceB = new List<bool>();
            
            // Extract LSBs in flatten order (like Python img.flatten())
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        lsbSequenceR.Add((pixel.R & 1) == 1);
                        lsbSequenceG.Add((pixel.G & 1) == 1);
                        lsbSequenceB.Add((pixel.B & 1) == 1);
                    }
                }
            });

            // Check for strong correlation between R, G, B LSB planes (Python embeds same data in all)
            var correlationScore = CalculateChannelCorrelation(lsbSequenceR, lsbSequenceG, lsbSequenceB);
            
            // Check for repeating patterns (Python repeats message across image)
            var repetitionScore = DetectRepetitionPattern(lsbSequenceR);
            
            // Combined score - high correlation + repetition suggests Python-style embedding
            var combinedScore = (correlationScore * 0.6) + (repetitionScore * 0.4);
            bool isSuspicious = combinedScore > PYTHON_LSB_THRESHOLD;

            return new TestResult
            {
                TestName = "Python LSB Pattern",
                Score = combinedScore,
                Threshold = PYTHON_LSB_THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Detects Python-style LSB embedding patterns",
                Interpretation = isSuspicious
                    ? $"Strong Python LSB pattern detected (correlation: {correlationScore:F3}, repetition: {repetitionScore:F3})"
                    : $"No Python LSB pattern detected (correlation: {correlationScore:F3}, repetition: {repetitionScore:F3})"
            };
        }

        private double CalculateChannelCorrelation(List<bool> r, List<bool> g, List<bool> b)
        {
            if (r.Count == 0) return 0;
            
            int matches = 0;
            int totalComparisons = 0;
            
            // Compare LSBs across channels (Python embeds same data in all channels)
            for (int i = 0; i < Math.Min(r.Count, Math.Min(g.Count, b.Count)); i++)
            {
                if (r[i] == g[i] && g[i] == b[i])
                    matches++;
                totalComparisons++;
            }
            
            return totalComparisons > 0 ? (double)matches / totalComparisons : 0;
        }

        private double DetectRepetitionPattern(List<bool> sequence)
        {
            if (sequence.Count < 64) return 0; // Need sufficient data
            
            var maxRepetitionScore = 0.0;
            
            // Test for repeating patterns of various lengths (8-120 bits = 1-15 chars)
            for (int patternLength = 64; patternLength <= 120 && patternLength < sequence.Count / 4; patternLength += 8)
            {
                var repetitionScore = CheckPatternRepetition(sequence, patternLength);
                maxRepetitionScore = Math.Max(maxRepetitionScore, repetitionScore);
            }
            
            return maxRepetitionScore;
        }

        private double CheckPatternRepetition(List<bool> sequence, int patternLength)
        {
            if (sequence.Count < patternLength * 2) return 0;
            
            var pattern = sequence.Take(patternLength).ToArray();
            int matches = 0;
            int totalChecks = 0;
            
            // Check how often the pattern repeats
            for (int start = patternLength; start + patternLength <= sequence.Count; start += patternLength)
            {
                var currentSegment = sequence.Skip(start).Take(patternLength).ToArray();
                if (currentSegment.Length == patternLength)
                {
                    var segmentMatches = pattern.Zip(currentSegment, (a, b) => a == b).Count(match => match);
                    matches += segmentMatches;
                    totalChecks += patternLength;
                }
            }
            
            return totalChecks > 0 ? (double)matches / totalChecks : 0;
        }

        private void CalculateOverallResult(DetectionResult result, FileInfo fileInfo)
        {
            var suspiciousTests = result.TestResults.Values.Count(t => t.IsSuspicious);
            var totalTests = result.TestResults.Count;
            
            // Calculate weighted confidence (some tests are more reliable than others)
            var weightedScore = 0.0;
            var totalWeight = 0.0;
            
            foreach (var test in result.TestResults)
            {
                var weight = GetTestWeight(test.Key);
                totalWeight += weight;
                if (test.Value.IsSuspicious)
                    weightedScore += weight;
            }
            
            result.OverallConfidence = totalWeight > 0 ? weightedScore / totalWeight : 0;
            
            // Very conservative detection criteria with special cases for strong indicators
            // Special case: Perfect or near-perfect entropy is extremely suspicious
            var entropyTest = result.TestResults.ContainsKey("Entropy") ? result.TestResults["Entropy"] : null;
            var hasHighEntropy = entropyTest?.Score >= 0.996; // Near perfect entropy (just below threshold)
            
            // Special case: If Python LSB test is highly suspicious, use slightly lower threshold
            var pythonLsbTest = result.TestResults.ContainsKey("PythonLSB") ? result.TestResults["PythonLSB"] : null;
            var hasPythonPattern = pythonLsbTest?.IsSuspicious == true && pythonLsbTest.Score > 0.3;
            
            var isPng = fileInfo.Extension.ToLowerInvariant() == ".png";
            var isJpeg = fileInfo.Extension.ToLowerInvariant() is ".jpg" or ".jpeg";
            
            // Strong steganography indicators (high entropy + any other suspicious test)
            if (hasHighEntropy && suspiciousTests >= 2)
            {
                result.IsSuspicious = true;
            }
            // Python pattern with high confidence + entropy
            else if (hasPythonPattern && hasHighEntropy)
            {
                result.IsSuspicious = true;
            }
            // Python pattern with very high confidence alone
            else if (hasPythonPattern && result.OverallConfidence > 0.7)
            {
                result.IsSuspicious = suspiciousTests >= 2 && result.OverallConfidence > 0.7;
            }
            else if (isPng)
            {
                // Even higher threshold for PNGs (more compression artifacts)
                result.IsSuspicious = suspiciousTests >= 4 && result.OverallConfidence > 0.8;
            }
            else if (isJpeg)
            {
                // Highest threshold for JPEGs (lossy compression creates artifacts)
                result.IsSuspicious = suspiciousTests >= 4 && result.OverallConfidence > 0.85;
            }
            else
            {
                // Standard very conservative threshold
                result.IsSuspicious = suspiciousTests >= 3 && result.OverallConfidence > 0.75;
            }
            
            // Much more conservative risk level classification
            if (result.OverallConfidence >= 0.95)
                result.RiskLevel = "Very High";
            else if (result.OverallConfidence >= 0.85)
                result.RiskLevel = "High";
            else if (result.OverallConfidence >= 0.7)
                result.RiskLevel = "Medium";
            else if (result.OverallConfidence >= 0.5)
                result.RiskLevel = "Low";
            else
                result.RiskLevel = "Very Low";

            // Generate summary with tuned thresholds info
            if (result.IsSuspicious)
            {
                var suspiciousTestNames = result.TestResults
                    .Where(t => t.Value.IsSuspicious)
                    .Select(t => t.Key)
                    .ToList();

                var detectionReason = "";
                if (hasHighEntropy && suspiciousTests >= 2)
                    detectionReason = " (High entropy + suspicious patterns detected)";
                else if (hasPythonPattern && hasHighEntropy)
                    detectionReason = " (Python LSB pattern + high entropy detected)";
                else if (hasPythonPattern)
                    detectionReason = " (Strong Python LSB pattern detected)";
                    
                result.Summary = $"⚠️ STEGANOGRAPHY DETECTED ({result.RiskLevel} Risk){detectionReason}\n" +
                               $"Suspicious tests: {string.Join(", ", suspiciousTestNames)} ({suspiciousTests}/{totalTests})\n" +
                               $"Weighted confidence: {result.OverallConfidence:P1}\n" +
                               $"Note: Enhanced detection for embedded steganography";
            }
            else
            {
                var suspiciousCount = result.TestResults.Values.Count(t => t.IsSuspicious);
                if (suspiciousCount > 0)
                {
                    result.Summary = $"✅ No steganography detected ({result.RiskLevel} Risk)\n" +
                                   $"Some tests flagged ({suspiciousCount}/{totalTests}) but below strict threshold\n" +
                                   $"Weighted confidence: {result.OverallConfidence:P1}\n" +
                                   $"Note: Enhanced algorithm with conservative thresholds";
                }
                else
                {
                    result.Summary = $"✅ No steganography detected\n" +
                                   $"All statistical tests passed\n" +
                                   $"Image appears clean";
                }
            }
        }

        private double GetTestWeight(string testName)
        {
            // Conservative weights - reduce impact of tests prone to false positives
            return testName switch
            {
                "ChiSquare" => 3.5,      // Chi-square is most reliable
                "SamplePair" => 0.6,     // Sample pair can be very noisy with natural textures
                "RSAnalysis" => 1.0,     // RS analysis is fairly reliable
                "Entropy" => 0.4,        // Entropy often misleading with natural images
                "Histogram" => 0.3,      // Histogram analysis prone to many false positives
                "PythonLSB" => 2.0,      // Highest weight for very specific Python patterns
                _ => 1.0
            };
        }

        #region Helper Methods

        private double CalculateVariance(List<byte> values)
        {
            if (values.Count == 0) return 0;
            
            var mean = values.Average(v => (double)v);
            return values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        }

        private double CalculateEntropy(List<byte> values)
        {
            if (values.Count == 0) return 0;
            
            var frequencies = values.GroupBy(v => v)
                                  .ToDictionary(g => g.Key, g => g.Count());
            
            var entropy = 0.0;
            foreach (var freq in frequencies.Values)
            {
                var probability = (double)freq / values.Count;
                if (probability > 0)
                    entropy -= probability * Math.Log2(probability);
            }
            
            return entropy;
        }

        #endregion
    }
} 