using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Histogram Analysis for LSB steganography detection
    /// Analyzes pixel value distribution for anomalies
    /// </summary>
    public class HistogramTest : IStatisticalTest
    {
        private const double THRESHOLD = 0.2; // Very conservative threshold to minimize false positives
        
        public string Name => "Histogram Analysis";
        public double Weight => 0.5; // Low weight - least reliable method

        public TestResult Execute(Image<Rgb24> image)
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
            bool isSuspicious = suspiciousRatio > THRESHOLD;

            return new TestResult
            {
                TestName = Name,
                Score = suspiciousRatio,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Analyzes pixel value distribution for anomalies",
                Interpretation = isSuspicious
                    ? "Unusual patterns in pixel value distribution"
                    : "Normal pixel value distribution"
            };
        }
    }
} 